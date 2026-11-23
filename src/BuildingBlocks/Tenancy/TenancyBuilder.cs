using Trislab.Framework.Shared.Multitenancy;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// Chooses the mode in code. Without a call, the mode comes from configuration (<c>TenancyOptions:Mode</c>,
/// default multi-tenant). A call wins over configuration.
/// </summary>
public sealed class TenancyBuilder
{
    internal TenancyBuilder(TenancyOptions options) => Options = options;

    internal TenancyOptions Options { get; }

    /// <summary>
    /// One fixed tenant, set in configuration (<c>TenancyOptions:SingleTenant</c>) or here. No tenant catalog,
    /// sign-up, plans or operator console; the data is still stamped with the tenant.
    /// </summary>
    public TenancyBuilder UseSingleTenant(Action<SingleTenantOptions>? configure = null)
    {
        Options.Mode = TenancyMode.SingleTenant;
        configure?.Invoke(Options.SingleTenant);
        return this;
    }

    /// <summary>
    /// Many tenants from a tenant catalog (the Multitenancy module). The default.
    /// </summary>
    public TenancyBuilder UseMultitenant()
    {
        Options.Mode = TenancyMode.Multitenant;
        return this;
    }
}
