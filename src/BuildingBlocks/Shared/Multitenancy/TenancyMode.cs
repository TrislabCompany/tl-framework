namespace Trislab.Framework.Shared.Multitenancy;

/// <summary>
/// How a host runs. <see cref="Multitenant"/> serves many tenants from a tenant catalog (a SaaS product).
/// <see cref="SingleTenant"/> serves one fixed tenant set in configuration (a webapp). Data is stamped with
/// the tenant in both modes, so module code is the same.
/// </summary>
public enum TenancyMode
{
    Multitenant = 0,
    SingleTenant = 1,
}

/// <summary>The modes a module supports, declared in code by <c>IModule.SupportedTenancyModes</c>.</summary>
[Flags]
public enum TenancyModes
{
    None = 0,
    Multitenant = 1,
    SingleTenant = 2,
    All = Multitenant | SingleTenant,
}

public static class TenancyModeExtensions
{
    /// <summary>The single <see cref="TenancyModes"/> flag that matches <paramref name="mode"/>.</summary>
    public static TenancyModes ToFlag(this TenancyMode mode) => mode switch
    {
        TenancyMode.Multitenant => TenancyModes.Multitenant,
        TenancyMode.SingleTenant => TenancyModes.SingleTenant,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown tenancy mode."),
    };

    /// <summary>True when <paramref name="supported"/> contains <paramref name="mode"/>.</summary>
    public static bool Supports(this TenancyModes supported, TenancyMode mode) => (supported & mode.ToFlag()) != 0;
}
