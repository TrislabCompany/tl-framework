using Trislab.Framework.Shared.Multitenancy;

namespace Trislab.Framework.Tenancy;

/// <summary>The fixed tenant built once from <see cref="SingleTenantOptions"/>.</summary>
internal sealed class SingleTenantProvider : ISingleTenantProvider
{
    // Far enough away that no grace-period arithmetic (ValidUpto + days) overflows, and no clock ever expires it.
    private static readonly DateTime NeverExpires = new(9000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public SingleTenantProvider(SingleTenantOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Tenant = new AppTenantInfo(options.Id, options.Id, options.Name)
        {
            ConnectionString = string.Empty,
            AdminEmail = options.AdminEmail,
            IsActive = true,
            ValidUpto = NeverExpires,
        };
        Country = options.Country.ToUpperInvariant();
    }

    public AppTenantInfo Tenant { get; }

    public string Country { get; }
}
