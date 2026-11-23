namespace Trislab.Framework.Shared.Multitenancy;

/// <summary>
/// The fixed tenant of a single-tenant host. Registered only in single-tenant mode, so a building block that
/// needs a tenant outside a request (a background job) asks for it optionally and finds none in multi-tenant mode.
/// </summary>
public interface ISingleTenantProvider
{
    AppTenantInfo Tenant { get; }

    /// <summary>ISO 3166-1 alpha-2 country code of the tenant.</summary>
    string Country { get; }
}
