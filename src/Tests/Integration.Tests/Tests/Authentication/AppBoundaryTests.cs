using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Authentication;

/// <summary>
/// The admin app is the operator console: only root-tenant accounts get a token through it, and the
/// dashboard is for tenant accounts only. The token endpoint enforces both through the X-Trislab-App
/// header; the root-only permissions on every operator endpoint are the real guard behind it.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class AppBoundaryTests
{
    private const string AppHeader = "X-Trislab-App";

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public AppBoundaryTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task IssueToken_Should_Succeed_When_RootSignsInToAdminApp()
    {
        using var response = await IssueTokenAsync(
            TestConstants.RootTenantId, "admin", TestConstants.RootAdminEmail, TestConstants.DefaultPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task IssueToken_Should_Return403_When_TenantSignsInToAdminApp()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantId = $"app-boundary-{unique}";
        var adminEmail = $"app-boundary-admin-{unique}@tenant.com";
        await CreateTenantAsync(rootClient, tenantId, adminEmail);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenantId);

        using var response = await IssueTokenAsync(tenantId, "admin", adminEmail, TestConstants.DefaultPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
            "a tenant's admin must not get a token for the operator console");
    }

    [Fact]
    public async Task IssueToken_Should_Return403_When_RootSignsInToDashboard()
    {
        using var response = await IssueTokenAsync(
            TestConstants.RootTenantId, "dashboard", TestConstants.RootAdminEmail, TestConstants.DefaultPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> IssueTokenAsync(string tenant, string app, string email, string password)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", tenant);
        request.Headers.Add(AppHeader, app);
        request.Content = JsonContent.Create(new { email, password });
        return await client.SendAsync(request);
    }

    private static async Task CreateTenantAsync(HttpClient rootClient, string tenantId, string adminEmail)
    {
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"Create tenant failed: {body}");
    }
}
