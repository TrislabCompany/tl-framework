using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using Integration.Tests.Infrastructure;
using Integration.Tests.Tests.Auditing;

namespace Integration.Tests.Tests.Multitenancy;

/// <summary>
/// D-44: the operator's actions on a tenant's record run in root and are audited there, with the operator as the
/// user. Every request is recorded by the HTTP audit middleware; this pins it for the tenant actions.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class OperatorTenantActionsAuditTests(TrislabWebApplicationFactory factory)
{
    private readonly AuthHelper _auth = new(factory);

    [Fact]
    public async Task Deactivating_And_Reactivating_A_Tenant_Should_Be_In_Roots_Audit_Trail()
    {
        // Arrange
        var tenant = $"audit-op-{Guid.NewGuid().ToString("N")[..8]}";
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var created = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenant,
            name = $"Audit {tenant}",
            connectionString = (string?)null,
            adminEmail = $"admin@{tenant}.test",
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenant}.issuer",
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenant);
        var rootUserId = new JwtSecurityTokenHandler()
            .ReadJwtToken((await _auth.GetRootAdminTokenAsync()).AccessToken).Subject;

        // Act
        var off = await rootClient.PostAsJsonAsync($"{TestConstants.TenantsBasePath}/{tenant}/activation", new { tenantId = tenant, isActive = false });
        var on = await rootClient.PostAsJsonAsync($"{TestConstants.TenantsBasePath}/{tenant}/activation", new { tenantId = tenant, isActive = true });
        off.StatusCode.ShouldBe(HttpStatusCode.OK, await off.Content.ReadAsStringAsync());
        on.StatusCode.ShouldBe(HttpStatusCode.OK, await on.Content.ReadAsStringAsync());

        // Assert — root's trail names the request and the operator who made it.
        var audit = await AuditTestHelper.PollForAuditAsync(
            rootClient,
            a => a.UserId == rootUserId,
            extraQuery: $"Search={Uri.EscapeDataString($"/tenants/{tenant}/activation")}");
        audit.TenantId.ShouldBe(TestConstants.RootTenantId);
    }
}
