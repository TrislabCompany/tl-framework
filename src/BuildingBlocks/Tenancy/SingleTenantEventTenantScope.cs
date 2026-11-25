using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// <see cref="IEventTenantScope"/> of a single-tenant host: every dispatch runs in the fixed tenant, whatever
/// id the event carries, so handler DbContexts built inside the scope always see a tenant. The tenant is in
/// memory; there is nothing to look up.
/// </summary>
public sealed class SingleTenantEventTenantScope : IEventTenantScope
{
    private readonly ITenantAccessor _tenants;
    private readonly ISingleTenantProvider _tenant;

    public SingleTenantEventTenantScope(ITenantAccessor tenants, ISingleTenantProvider tenant)
    {
        _tenants = tenants;
        _tenant = tenant;
    }

    public IDisposable Begin(string? tenantId) => _tenants.Use(_tenant.Tenant);
}
