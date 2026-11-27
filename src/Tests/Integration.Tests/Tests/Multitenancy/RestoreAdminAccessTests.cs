#pragma warning disable S1144 // Unused private members — populated by JSON
#pragma warning disable S3459 // Unassigned members — populated by JSON
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Multitenancy;

/// <summary>
/// D-44: a platform operator gives a tenant an Admin again, after checking with the customer. An active member
/// becomes Admin, or the address is invited as Admin; the tenant's active Admins are told by e-mail. Nothing
/// else changes, and only root may do it.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class RestoreAdminAccessTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    private string _tenant = default!;
    private string _adminEmail = default!;

    public RestoreAdminAccessTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    public async ValueTask InitializeAsync()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        _tenant = $"restore-{unique}";
        _adminEmail = $"admin-{unique}@restore.test";

        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var created = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = _tenant,
            name = $"Restore {unique}",
            connectionString = (string?)null,
            adminEmail = _adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{_tenant}.issuer",
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, _tenant);
        _ = await TokenWithRetryAsync(_adminEmail, _tenant);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string RestorePath(string tenant) => $"{TestConstants.IdentityBasePath}/tenants/{tenant}/restore-admin";

    [Fact]
    public async Task Restore_Should_MakeAnActiveMemberAdmin_And_TellTheAdmins()
    {
        // Arrange — a Basic member of the tenant, e.g. the colleague of an Admin who left.
        using var adminClient = await _auth.CreateAuthenticatedClientAsync(_adminEmail, TestConstants.DefaultPassword, _tenant);
        var member = await TestUsers.InviteAndAcceptAsync(_factory, adminClient, "restore-member");
        using var rootClient = await _auth.CreateRootAdminClientAsync();

        // Act
        var response = await rootClient.PostAsJsonAsync(RestorePath(_tenant), new
        {
            email = member.Email,
            reason = "Ticket 1234: the only Admin left the company",
        });

        // Assert — the member is Admin now, and the existing Admin got the notice.
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<RestoreResponse>(Json);
        body!.Outcome.ShouldBe("RoleAssigned");
        body.AdminsNotified.ShouldBe(1);

        var roles = await adminClient.GetFromJsonAsync<List<UserRole>>(
            $"{TestConstants.IdentityBasePath}/users/{member.UserId}/roles", Json);
        roles!.ShouldContain(r => r.RoleName == "Admin" && r.Enabled);

        var notice = await Mailpit.WaitForMessageAsync(_adminEmail, m => m.Body.Contains("restored admin access", StringComparison.Ordinal));
        notice.Body.ShouldContain(member.Email);
    }

    [Fact]
    public async Task Restore_Should_InviteANewAddressAsAdmin()
    {
        // Arrange
        var email = $"new-admin-{Guid.NewGuid():N}@restore.test";
        using var rootClient = await _auth.CreateRootAdminClientAsync();

        // Act — invited as Admin, then the owner accepts.
        var response = await rootClient.PostAsJsonAsync(RestorePath(_tenant), new
        {
            email,
            reason = "Ticket 1235: nobody can sign in as Admin",
            firstName = "New",
            lastName = "Admin",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<RestoreResponse>(Json))!.Outcome.ShouldBe("Invited");

        var token = await TestUsers.WaitForInvitationTokenAsync(email);
        using var anonymous = _factory.CreateClient();
        var accept = await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new
        {
            token,
            password = TestUsers.DefaultUserPassword,
            firstName = "New",
            lastName = "Admin",
        });
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());

        // Assert — signed in to the tenant, the new member holds the Admin role.
        var signedIn = await _auth.GetTokenAsync(email, TestUsers.DefaultUserPassword, _tenant);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(signedIn.AccessToken);
        jwt.Claims.ShouldContain(c => (c.Type == "role" || c.Type.EndsWith("/role", StringComparison.Ordinal)) && c.Value == "Admin");
    }

    [Fact]
    public async Task Restore_Should_Return403_When_CallerIsATenantAdmin()
    {
        using var adminClient = await _auth.CreateAuthenticatedClientAsync(_adminEmail, TestConstants.DefaultPassword, _tenant);

        var response = await adminClient.PostAsJsonAsync(RestorePath(_tenant), new
        {
            email = $"self-help-{Guid.NewGuid():N}@restore.test",
            reason = "a tenant admin tries the operator action",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(TestConstants.RootTenantId)]
    [InlineData("no-such-tenant")]
    public async Task Restore_Should_Return404_When_TenantIsRootOrUnknown(string tenant)
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();

        var response = await rootClient.PostAsJsonAsync(RestorePath(tenant), new
        {
            email = $"nobody-{Guid.NewGuid():N}@restore.test",
            reason = "wrong tenant",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_Should_RequireAReason()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();

        var response = await rootClient.PostAsJsonAsync(RestorePath(_tenant), new
        {
            email = $"no-reason-{Guid.NewGuid():N}@restore.test",
            reason = "",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<TokenResult> TokenWithRetryAsync(string email, string tenant)
    {
        Exception? last = null;
        for (var i = 0; i < 30; i++)
        {
            try
            {
                return await _auth.GetTokenAsync(email, TestConstants.DefaultPassword, tenant);
            }
            catch (HttpRequestException ex)
            {
                last = ex;
                await Task.Delay(500);
            }
        }
        throw last ?? new InvalidOperationException("token issuance failed");
    }

    private sealed class RestoreResponse
    {
        public string Outcome { get; set; } = default!;
        public int AdminsNotified { get; set; }
    }

    private sealed class UserRole
    {
        public string? RoleName { get; set; }
        public bool Enabled { get; set; }
    }
}
