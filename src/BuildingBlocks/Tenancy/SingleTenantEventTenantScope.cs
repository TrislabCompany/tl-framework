using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
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
    private readonly IMultiTenantContextAccessor<AppTenantInfo> _accessor;
    private readonly IMultiTenantContextSetter _setter;
    private readonly ISingleTenantProvider _tenant;

    public SingleTenantEventTenantScope(
        IMultiTenantContextAccessor<AppTenantInfo> accessor,
        IMultiTenantContextSetter setter,
        ISingleTenantProvider tenant)
    {
        _accessor = accessor;
        _setter = setter;
        _tenant = tenant;
    }

    public IDisposable Begin(string? tenantId)
    {
        var previous = _accessor.MultiTenantContext;
        _setter.MultiTenantContext = new MultiTenantContext<AppTenantInfo>(_tenant.Tenant);
        return new RestoreScope(_setter, previous);
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly IMultiTenantContextSetter _setter;
        private readonly IMultiTenantContext<AppTenantInfo> _previous;

        public RestoreScope(IMultiTenantContextSetter setter, IMultiTenantContext<AppTenantInfo> previous)
        {
            _setter = setter;
            _previous = previous;
        }

        public void Dispose() => _setter.MultiTenantContext = _previous;
    }
}
