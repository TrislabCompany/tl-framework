using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Multitenancy;

[Collection(TrislabCollectionDefinition.Name)]
public sealed class TenantIsolationTests
{
    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public TenantIsolationTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task TenantIsolation_Should_NotLeakUsers_When_QueryingAcrossTenants()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        // Create and provision a new tenant
        var tenantId = $"iso-{uniqueId}";
        var tenantAdminEmail = $"isoadmin-{uniqueId}@tenant.com";
        await CreateTenantAsync(rootClient, tenantId, tenantAdminEmail);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenantId);

        // Login as new tenant admin — retry because provisioning may still be finalizing
        using var tenantClient = await CreateTenantAdminClientWithRetryAsync(
            tenantAdminEmail, TestConstants.DefaultPassword, tenantId);

        var isolated = await TestUsers.InviteAndAcceptAsync(_factory, tenantClient, "isolated", "Isolated", "User");

        // Query users from root tenant — isolated user must NOT appear
        var rootUsersResponse = await rootClient.GetAsync(
            $"{TestConstants.IdentityBasePath}/users/search?pageNumber=1&pageSize=100");
        rootUsersResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rootUsersContent = await rootUsersResponse.Content.ReadAsStringAsync();
        rootUsersContent.ShouldNotContain(isolated.Email);
    }

    [Fact]
    public async Task TenantIsolation_Should_AllowSameEmailInDifferentTenants()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        string sharedEmail = $"shared-{uniqueId}@example.com";

        // Create two tenants
        var tenant1Id = $"dup1-{uniqueId}";
        var tenant2Id = $"dup2-{uniqueId}";
        var t1AdminEmail = $"t1admin-{uniqueId}@tenant.com";
        var t2AdminEmail = $"t2admin-{uniqueId}@tenant.com";

        await CreateTenantAsync(rootClient, tenant1Id, t1AdminEmail);
        await CreateTenantAsync(rootClient, tenant2Id, t2AdminEmail);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenant1Id);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenant2Id);

        // Invite the same email into tenant 1
        using var client1 = await CreateTenantAdminClientWithRetryAsync(
            t1AdminEmail, TestConstants.DefaultPassword, tenant1Id);
        await TestUsers.InviteAsync(client1, sharedEmail, "Shared", "Email");

        // Invite the same email into tenant 2 — should succeed
        using var client2 = await CreateTenantAdminClientWithRetryAsync(
            t2AdminEmail, TestConstants.DefaultPassword, tenant2Id);
        await TestUsers.InviteAsync(client2, sharedEmail, "Shared", "Email");
    }

    private async Task<HttpClient> CreateTenantAdminClientWithRetryAsync(
        string email, string password, string tenant, int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await _auth.CreateAuthenticatedClientAsync(email, password, tenant);
            }
            catch (HttpRequestException) when (i < maxRetries - 1)
            {
                await Task.Delay(1000);
            }
        }

        return await _auth.CreateAuthenticatedClientAsync(email, password, tenant);
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
            issuer = $"{tenantId}.issuer"
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, $"Create tenant failed: {body}");
    }
}
