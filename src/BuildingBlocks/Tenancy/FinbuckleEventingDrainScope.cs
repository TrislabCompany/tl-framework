using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// Installs an <see cref="AppTenantInfo"/> carrying the tenant's connection string, so an
/// <c>EventingDbContext</c> built inside the scope routes to that tenant's database.
///
/// Deliberately distinct from <see cref="FinbuckleEventTenantScope"/>, which sets tenant identity
/// only: that scope wraps handler dispatch, where the row-level tenant filter is what matters.
/// This one wraps a drain pass, where the target <i>database</i> is what matters.
/// </summary>
public sealed class FinbuckleEventingDrainScope : IEventingDrainScope
{
    private readonly ITenantAccessor _tenants;

    public FinbuckleEventingDrainScope(ITenantAccessor tenants) => _tenants = tenants;

    public IDisposable Begin(EventingDrainTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.TenantId is null && target.ConnectionString is null)
        {
            // Default pass: leave the ambient context alone so the context falls through to the
            // configured default connection.
            return NoopScope.Instance;
        }

        // Built by hand rather than via the tenant-shaped constructor: only the id and the
        // connection string matter for routing, and the richer constructor also stamps validity
        // and activation state we have no business inventing here.
        var info = new AppTenantInfo(target.TenantId!, target.TenantId!)
        {
            ConnectionString = target.ConnectionString ?? string.Empty,
        };

        return _tenants.Use(info);
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
            // Nothing to restore — the ambient context was never touched.
        }
    }
}
