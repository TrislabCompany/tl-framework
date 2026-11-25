using Finbuckle.MultiTenant;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// Returns the default database plus one target per distinct active per-tenant connection string.
///
/// Tenants sharing a database collapse to a single target: outbox rows are <c>IGlobalEntity</c>
/// and carry an explicit TenantId column, so one pass over a database drains every tenant living
/// in it. Draining once per tenant instead would multiply the work and have several passes race
/// for the same rows.
/// </summary>
public sealed class TenantStoreDrainTargetProvider : IEventingDrainTargetProvider
{
    private readonly TenantManager<AppTenantInfo, string> _tenants;

    public TenantStoreDrainTargetProvider(TenantManager<AppTenantInfo, string> tenants)
        => _tenants = tenants;

    public async Task<IReadOnlyList<EventingDrainTarget>> GetTargetsAsync(CancellationToken ct = default)
    {
        var tenants = await _tenants.GetAllAsync(ct).ConfigureAwait(false);

        var dedicated = tenants
            .Where(t => t.IsActive && !string.IsNullOrWhiteSpace(t.ConnectionString))
            .GroupBy(t => t.ConnectionString, StringComparer.Ordinal)
            .Select(g => new EventingDrainTarget(g.First().Id, g.Key));

        // The default pass always runs: it covers every tenant without a dedicated database.
        return [new EventingDrainTarget(null, null), .. dedicated];
    }
}
