using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Authentication;

/// <summary>
/// Users are global: one account per e-mail address, with a membership in each tenant. Sign-in needs no
/// tenant: it opens the tenant the header names, else the default, else the only one, and otherwise
/// answers with the list to choose from. Operators stay in root.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed partial class GlobalUserSignInTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public GlobalUserSignInTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task A_User_In_Two_Tenants_Chooses_Then_Opens_The_Default_And_Switches()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("two");

        // No tenant and no default: the user chooses.
        var first = await SignInAsync(user.Email, user.Password);
        first.TenantSelectionRequired.ShouldBeTrue();
        first.AccessToken.ShouldBeNull();
        first.Tenants!.Select(t => t.Id).ShouldBe([tenantA, tenantB], ignoreOrder: true);
        first.SelectionToken.ShouldNotBeNullOrWhiteSpace();

        var selected = await PostAsync<SignInResult>("/token/select", new { selectionToken = first.SelectionToken, tenant = tenantA, setDefault = true });
        selected.Tenant.ShouldBe(tenantA);
        TenantClaim(selected.AccessToken!).ShouldBe(tenantA);

        // The default now opens directly.
        var second = await SignInAsync(user.Email, user.Password);
        second.TenantSelectionRequired.ShouldBeFalse();
        TenantClaim(second.AccessToken!).ShouldBe(tenantA);

        // The menu lists both tenants with the default marked, and the switch issues tokens for the other.
        using var client = Bearer(second.AccessToken!);
        var mine = await client.GetFromJsonAsync<List<TenantChoice>>($"{TestConstants.IdentityBasePath}/profile/tenants", JsonOptions);
        mine!.Single(t => t.Id == tenantA).IsDefault.ShouldBeTrue();
        mine!.Single(t => t.Id == tenantB).IsDefault.ShouldBeFalse();

        using var switchResponse = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB });
        switchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await switchResponse.Content.ReadAsStringAsync());
        var switched = await switchResponse.Content.ReadFromJsonAsync<SignInResult>(JsonOptions);
        TenantClaim(switched!.AccessToken!).ShouldBe(tenantB);

        // The tenant header still picks a tenant the user belongs to.
        var viaHeader = await SignInAsync(user.Email, user.Password, tenantB);
        TenantClaim(viaHeader.AccessToken!).ShouldBe(tenantB);
    }

    [Fact]
    public async Task A_Refresh_Needs_No_Tenant_And_Keeps_The_Tenant()
    {
        var (_, tenantB, user) = await CreateUserInTwoTenantsAsync("refresh");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantB);

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/token/refresh", new { refreshToken = signedIn.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var refreshed = await response.Content.ReadFromJsonAsync<TokenRefreshResult>(JsonOptions);
        TenantClaim(refreshed!.Token).ShouldBe(tenantB);
    }

    [Fact]
    public async Task SignIn_Should_Return401_For_A_Tenant_The_User_Is_Not_A_Member_Of()
    {
        var (_, _, user) = await CreateUserInTwoTenantsAsync("stranger");

        using var response = await IssueAsync(user.Email, user.Password, TestConstants.RootTenantId);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Deactivated_Membership_Leaves_The_Other_Tenant()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("deact");
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);

        using var deactivate = new HttpRequestMessage(HttpMethod.Patch, $"{TestConstants.IdentityBasePath}/users/{user.UserId}")
        {
            Content = JsonContent.Create(new { activateUser = false }),
        };
        (await adminA.SendAsync(deactivate)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Only tenant B is left, so sign-in opens it without a choice.
        var signedIn = await SignInAsync(user.Email, user.Password);
        signedIn.TenantSelectionRequired.ShouldBeFalse();
        TenantClaim(signedIn.AccessToken!).ShouldBe(tenantB);

        (await IssueAsync(user.Email, user.Password, tenantA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_Operator_Signs_In_To_Root_Without_A_Tenant_And_Cannot_Join_A_Tenant()
    {
        var root = await SignInAsync(TestConstants.RootAdminEmail, TestConstants.DefaultPassword);
        TenantClaim(root.AccessToken!).ShouldBe(TestConstants.RootTenantId);

        // A tenant admin may invite the operator's address (the answer reveals nothing), but the operator can't accept.
        var tenantId = await CreateTenantAsync("op");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var before = await TestUsers.InvitationCountAsync(TestConstants.RootAdminEmail);
        await TestUsers.InviteAsync(admin, TestConstants.RootAdminEmail);
        var token = await TestUsers.WaitForInvitationTokenAsync(TestConstants.RootAdminEmail, before);

        using var operatorClient = Bearer(root.AccessToken!);
        using var response = await operatorClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "operator accounts stay in the root tenant");
        (await response.Content.ReadAsStringAsync()).ShouldContain("Identity.OperatorAccountsStayInRoot");
    }

    [Fact]
    public async Task A_Tenant_Admin_Cannot_Reach_A_User_Of_Another_Tenant_By_Id()
    {
        var tenantA = await CreateTenantAsync("isoa");
        var (_, tenantB, user) = await CreateUserInTwoTenantsAsync("isob");
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);

        // The user is a member of other tenants, not of A: by id, A must not find them.
        (await adminA.GetAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}/roles")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await adminA.GetAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        tenantB.ShouldNotBe(tenantA);
    }

    [Fact]
    public async Task A_Refresh_Token_Works_Once()
    {
        var (_, tenantB, user) = await CreateUserInTwoTenantsAsync("once");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantB);

        using var client = _factory.CreateClient();
        var body = new { refreshToken = signedIn.RefreshToken };
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/refresh", body)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/refresh", body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Switch_Should_Return403_For_A_Tenant_The_User_Is_Not_A_Member_Of()
    {
        var (_, tenantB, user) = await CreateUserInTwoTenantsAsync("swno");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantB);
        using var client = Bearer(signedIn.AccessToken!);

        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = TestConstants.RootTenantId });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Select_Should_Return401_For_A_Forged_Selection_Token()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/token/select", new { selectionToken = "not-a-token", tenant = "root" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(string TenantA, string TenantB, InvitedUser User)> CreateUserInTwoTenantsAsync(string prefix)
    {
        var tenantA = await CreateTenantAsync($"{prefix}a");
        var tenantB = await CreateTenantAsync($"{prefix}b");

        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);
        var user = await TestUsers.InviteAndAcceptAsync(_factory, adminA, $"{prefix}-user");

        // Tenant B invites the same address: the existing account accepts while signed in, keeping its password.
        using var adminB = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantB), TestConstants.DefaultPassword, tenantB);
        var token = await InviteExistingAsync(adminB, user.Email);
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        using var owner = Bearer(signedIn.AccessToken!);
        using var accept = await owner.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token });
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());

        return (tenantA, tenantB, user);
    }

    private static async Task<string> InviteExistingAsync(HttpClient admin, string email)
    {
        var before = await TestUsers.InvitationCountAsync(email);
        await TestUsers.InviteAsync(admin, email, "Other", "Name");
        return await TestUsers.WaitForInvitationTokenAsync(email, before);
    }

    private static string AdminEmail(string tenantId) => $"admin-{tenantId}@tenant.com";

    private async Task<string> CreateTenantAsync(string prefix)
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var tenantId = $"gu-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail = AdminEmail(tenantId),
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenantId);
        return tenantId;
    }

    private async Task<HttpResponseMessage> IssueAsync(string email, string password, string? tenant = null)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        if (tenant is not null)
        {
            request.Headers.Add("tenant", tenant);
        }

        return await client.SendAsync(request);
    }

    private async Task<SignInResult> SignInAsync(string email, string password, string? tenant = null)
    {
        using var response = await IssueAsync(email, password, tenant);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SignInResult>(JsonOptions))!;
    }

    private async Task<T> PostAsync<T>(string path, object body)
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}{path}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private HttpClient Bearer(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string? TenantClaim(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.FirstOrDefault(c => c.Type == "tenant")?.Value;

    private sealed record SignInResult(
        string? AccessToken,
        string? RefreshToken,
        string? Tenant,
        bool TenantSelectionRequired,
        string? SelectionToken,
        List<TenantChoice>? Tenants);

    private sealed record TenantChoice(string Id, string? Name, bool IsDefault);
}

/// <summary>In single-tenant mode every user belongs to the fixed tenant: sign-in needs no tenant at all.</summary>
[Collection(SingleTenantCollectionDefinition.Name)]
public sealed class GlobalUserSignInSingleTenantTests(SingleTenantWebApplicationFactory factory)
{
    [Fact]
    public async Task SignIn_Should_Open_The_Fixed_Tenant()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/token/issue",
            new { email = TestConstants.SingleTenantAdminEmail, password = TestConstants.DefaultPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain($"\"tenant\":\"{TestConstants.SingleTenantId}\"");
    }
}
