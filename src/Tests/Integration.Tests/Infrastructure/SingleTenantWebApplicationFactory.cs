using Trislab.Framework.Shared.Multitenancy;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// The same host as <see cref="TrislabWebApplicationFactory"/>, run in single-tenant mode: one fixed tenant from
/// configuration, no tenant catalog, no Multitenancy or Billing module. Tests that run in both modes take the
/// base type and sign in through <see cref="AuthHelper"/>.
/// </summary>
public sealed class SingleTenantWebApplicationFactory : TrislabWebApplicationFactory
{
    public override TenancyMode Mode => TenancyMode.SingleTenant;
}
