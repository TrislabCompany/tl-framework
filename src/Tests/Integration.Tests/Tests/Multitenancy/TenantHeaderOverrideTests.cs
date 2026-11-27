#pragma warning disable S1144 // Unused private members — populated by JSON
#pragma warning disable S3459 // Unassigned members — populated by JSON
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Trislab.Framework.Caching;
using Integration.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Multitenancy;

/// <summary>
/// Regression coverage for the tenant resolution chain. A root operator must be
/// able to scope a request to another tenant by sending the `tenant` header
/// (e.g. for cross-tenant user search before impersonation). A tenant operator
/// must NOT be able to do the same — that would let them browse other tenants'
/// data simply by setting a header.
///
/// These tests pin the contract enforced by the RootOperatorHeaderOverride
/// delegate strategy in <c>MultitenancyModule</c>.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class TenantHeaderOverrideTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    private string _tenantA = default!;
    private string _tenantAAdminEmail = default!;
    private string _tenantB = default!;
    private string _tenantBAdminEmail = default!;

    public TenantHeaderOverrideTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    public async ValueTask InitializeAsync()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        _tenantA = $"thot-a-{unique}";
        _tenantB = $"thot-b-{unique}";
        _tenantAAdminEmail = $"admin-a-{unique}@thot.com";
        _tenantBAdminEmail = $"admin-b-{unique}@thot.com";

        using var rootClient = await _auth.CreateRootAdminClientAsync();
        await CreateTenantAsync(rootClient, _tenantA, _tenantAAdminEmail);
        await CreateTenantAsync(rootClient, _tenantB, _tenantBAdminEmail);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, _tenantA);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, _tenantB);

        // Provisioning can report "Completed" a tick before the seeded admin user is queryable; a
        // successful token issuance cross-checks the user exists, avoiding a first-test race on empty lists.
        _ = await GetTokenWithRetryAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        _ = await GetTokenWithRetryAsync(_tenantBAdminEmail, TestConstants.DefaultPassword, _tenantB);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RootOperator_Should_TargetOtherTenant_When_HeaderProvided()
    {
        // Arrange — root admin is in `root` but sends `tenant: <tenantA>` header
        // to scope this single request to tenant A.
        var rootToken = await _auth.GetRootAdminTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", rootToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantA);

        // Act — search users with no filter. Should return tenant A's admin user.
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/search?PageNumber=1&PageSize=50");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SearchUserDto>>(Json);
        page.ShouldNotBeNull();
        // Tenant A's admin should be visible.
        page.Items.ShouldContain(u => u.Email == _tenantAAdminEmail);
        // Root-tenant users (admin@root.com) must NOT leak through.
        page.Items.ShouldNotContain(u => u.Email == TestConstants.RootAdminEmail);
        // Tenant B's users must NOT leak through.
        page.Items.ShouldNotContain(u => u.Email == _tenantBAdminEmail);
    }

    [Fact]
    public async Task RootOperator_Should_TargetOtherTenant_When_PermissionCacheIsCold()
    {
        // Arrange — evict the root admin's cached permission set, as its 2-minute local expiry (or any
        // role/group invalidation) does, so the next permission check has to load it again. The cache key
        // carries no tenant, and this request resolves to tenant A.
        var rootToken = await _auth.GetRootAdminTokenAsync();
        var rootUserId = new JwtSecurityTokenHandler().ReadJwtToken(rootToken.AccessToken)
            .Claims.First(c => c.Type is ClaimTypes.NameIdentifier or "nameid").Value;
        await _factory.Services.GetRequiredService<HybridCache>()
            .RemoveAsync(CacheKeys.UserPermissions(rootUserId));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", rootToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantA);

        // Act
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/search?PageNumber=1&PageSize=50");

        // Assert — the root user does not exist in tenant A, so a load under the request's tenant 401s.
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SearchUserDto>>(Json);
        page.ShouldNotBeNull();
        page.Items.ShouldContain(u => u.Email == _tenantAAdminEmail);
    }

    [Fact]
    public async Task RootOperator_Should_UseOwnTenant_When_NoHeaderSent()
    {
        // Arrange — bare CreateRootAdminClient sends `tenant: root`; claim matches header, so the
        // override no-ops and resolution scopes to the root tenant.
        using var rootClient = await _auth.CreateRootAdminClientAsync();

        // Act
        var response = await rootClient.GetAsync($"{TestConstants.IdentityBasePath}/users/search?PageNumber=1&PageSize=50");

        // Assert — root admin is in the result set; tenant A / B admins are not.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SearchUserDto>>(Json);
        page.ShouldNotBeNull();
        page.Items.ShouldContain(u => u.Email == TestConstants.RootAdminEmail);
        page.Items.ShouldNotContain(u => u.Email == _tenantAAdminEmail);
    }

    [Fact]
    public async Task TenantAdmin_HeaderOverride_Should_BeIgnored()
    {
        // Arrange — tenant A's admin sends a `tenant: B` header; the override is gated by claim==root,
        // so it must fail closed and the query stays in tenant A.
        var tenantAToken = await GetTokenWithRetryAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tenantAToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantB);

        // Act
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/search?PageNumber=1&PageSize=50");

        // Assert — still resolves to tenant A (claim wins): A's admin present, B's absent. This flips
        // if the override ever leaked to non-root callers.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SearchUserDto>>(Json);
        page.ShouldNotBeNull();
        page.Items.ShouldContain(u => u.Email == _tenantAAdminEmail);
        page.Items.ShouldNotContain(u => u.Email == _tenantBAdminEmail);
    }

    [Fact]
    public async Task RootOperator_Should_ReadATenantMembersRoles_When_HeaderProvided()
    {
        // The admin console's tenant page lists the tenant's users and their roles there, read only, through
        // this override: the operator never joins the tenant and needs no impersonation to look.
        var rootToken = await _auth.GetRootAdminTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", rootToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantA);
        var adminId = await FindUserIdAsync(client, _tenantAAdminEmail);

        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/{adminId}/roles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var roles = await response.Content.ReadFromJsonAsync<List<UserRoleDto>>(Json);
        roles.ShouldNotBeNull();
        roles.ShouldContain(r => r.Enabled && r.RoleName == "Admin");
    }

    [Fact]
    public async Task TenantAdmin_Should_NotReadAnotherTenantsMemberRoles_ThroughTheHeader()
    {
        // Tenant B's admin sends `tenant: A` and asks for tenant A's admin's roles: the claim wins, the request
        // stays in tenant B, where that user doesn't exist.
        string adminAId;
        var rootToken = await _auth.GetRootAdminTokenAsync();
        using (var rootClient = _factory.CreateClient())
        {
            rootClient.DefaultRequestHeaders.Authorization = new("Bearer", rootToken.AccessToken);
            rootClient.DefaultRequestHeaders.Add("tenant", _tenantA);
            adminAId = await FindUserIdAsync(rootClient, _tenantAAdminEmail);
        }

        var tenantBToken = await GetTokenWithRetryAsync(_tenantBAdminEmail, TestConstants.DefaultPassword, _tenantB);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tenantBToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantA);

        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/{adminAId}/roles");

        // The user isn't a member of tenant B, so B sees no such user.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TenantAdmin_Should_AccessOwnTenant_Normally()
    {
        // Arrange — sanity check that the new strategy doesn't break the
        // normal (no-override) path for tenant operators.
        using var tenantAClient = await _auth.CreateAuthenticatedClientAsync(
            _tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);

        // Act
        var response = await tenantAClient.GetAsync($"{TestConstants.IdentityBasePath}/users/search?PageNumber=1&PageSize=50");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SearchUserDto>>(Json);
        page.ShouldNotBeNull();
        page.Items.ShouldContain(u => u.Email == _tenantAAdminEmail);
    }

    // ─── D-44: the operator only reads inside another tenant ────────────

    [Fact]
    public async Task RootOperator_Should_BeRefused_When_WritingThroughHeader()
    {
        // Arrange — the root admin reads tenant A's theme through the header, then tries to write it back.
        var rootToken = await _auth.GetRootAdminTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", rootToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantA);
        var themePath = $"{TestConstants.TenantsBasePath}/theme";
        var before = await client.GetStringAsync(themePath);
        var invitee = $"through-header-{Guid.NewGuid():N}@thot.com";

        // Act — a PUT, a POST and a DELETE into tenant A.
        using var themeBody = new StringContent(before, System.Text.Encoding.UTF8, "application/json");
        var put = await client.PutAsync(themePath, themeBody);
        var reset = await client.PostAsync($"{themePath}/reset", content: null);
        var invite = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new { email = invitee });
        var delete = await client.DeleteAsync($"{TestConstants.IdentityBasePath}/users/{Guid.NewGuid()}");

        // Assert — each refused with the coded 403, and tenant A unchanged.
        foreach (var response in new[] { put, reset, invite, delete })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            problem.GetProperty("code").GetString().ShouldBe("Multitenancy.RootReadOnly");
        }

        (await client.GetStringAsync(themePath)).ShouldBe(before);
        using var tenantAAdmin = await _auth.CreateAuthenticatedClientAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        var invitations = await tenantAAdmin.GetStringAsync($"{TestConstants.IdentityBasePath}/invitations?PageNumber=1&PageSize=100");
        invitations.ShouldNotContain(invitee);
    }

    [Fact]
    public async Task RootOperator_Should_GetAnSseToken_Through_Header()
    {
        // The SSE stream token changes no tenant data, so it stays allowed while reading another tenant.
        var rootToken = await _auth.GetRootAdminTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", rootToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantA);

        var response = await client.PostAsync("/api/v1/sse/token", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RootOperator_Should_StillWrite_InRoot_Without_Override()
    {
        // The read-only rule is about other tenants: root's own writes (here an invitation into root) still work.
        using var rootClient = await _auth.CreateRootAdminClientAsync();

        var response = await rootClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new
        {
            email = $"root-own-{Guid.NewGuid():N}@thot.com",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task TenantAdmin_Should_StillWrite_When_HeaderNamesAnotherTenant()
    {
        // A tenant admin's header is ignored (claim wins), so the read-only rule never applies to them.
        var tenantAToken = await GetTokenWithRetryAsync(_tenantAAdminEmail, TestConstants.DefaultPassword, _tenantA);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tenantAToken.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", _tenantB);

        var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new
        {
            email = $"tenant-a-own-{Guid.NewGuid():N}@thot.com",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    // ─── helpers ────────────────────────────────────────────────────────

    private async Task<TokenResult> GetTokenWithRetryAsync(string email, string password, string tenant, int maxRetries = 30)
    {
        Exception? last = null;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return await _auth.GetTokenAsync(email, password, tenant);
            }
            catch (HttpRequestException ex)
            {
                last = ex;
                await Task.Delay(500);
            }
        }
        throw last ?? new InvalidOperationException("token issuance failed");
    }

    private static async Task CreateTenantAsync(HttpClient rootClient, string tenantId, string adminEmail)
    {
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"THOT {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<string> FindUserIdAsync(HttpClient client, string email)
    {
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/search?PageNumber=1&PageSize=50");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SearchUserDto>>(Json);
        var id = page?.Items.FirstOrDefault(u => u.Email == email)?.Id;
        id.ShouldNotBeNullOrEmpty();
        return id;
    }

    private sealed class UserRoleDto
    {
        public string? RoleId { get; set; }
        public string? RoleName { get; set; }
        public bool Enabled { get; set; }
    }

    private sealed class SearchUserDto
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
    }
}
