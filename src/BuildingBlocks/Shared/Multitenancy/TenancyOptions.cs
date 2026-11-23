using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Trislab.Framework.Shared.Multitenancy;

/// <summary>
/// The tenancy settings, section <c>TenancyOptions</c>. <see cref="Mode"/> defaults to
/// <see cref="TenancyMode.Multitenant"/>, so a host that sets nothing behaves as before.
/// </summary>
public sealed class TenancyOptions : IValidatableObject
{
    public const string SectionName = nameof(TenancyOptions);

    /// <summary>Multitenant (default) or SingleTenant.</summary>
    public TenancyMode Mode { get; set; } = TenancyMode.Multitenant;

    /// <summary>The fixed tenant. Read only when <see cref="Mode"/> is <see cref="TenancyMode.SingleTenant"/>.</summary>
    public SingleTenantOptions SingleTenant { get; set; } = new();

    /// <summary>Reads the mode from configuration without binding the whole section.</summary>
    public static TenancyMode ReadMode(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetSection(SectionName).Get<TenancyOptions>()?.Mode ?? TenancyMode.Multitenant;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Mode))
        {
            yield return new ValidationResult($"{SectionName}:Mode must be {nameof(TenancyMode.Multitenant)} or {nameof(TenancyMode.SingleTenant)}.");
        }

        if (Mode != TenancyMode.SingleTenant)
        {
            yield break;
        }

        foreach (var error in SingleTenant.Validate())
        {
            yield return new ValidationResult($"{SectionName}:SingleTenant: {error}");
        }
    }
}

/// <summary>The one fixed tenant of a single-tenant host: <c>TenancyOptions:SingleTenant</c>.</summary>
public sealed partial class SingleTenantOptions
{
    /// <summary>The tenant id stamped on every row and carried in the token claim. Lower-case letters, digits and hyphens.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The display name of the organisation.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code. Selects the country rule pack.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>The e-mail of the first administrator, seeded by the migrator.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    public IEnumerable<string> Validate()
    {
        if (!IdPattern().IsMatch(Id))
        {
            yield return "Id must be 1 to 64 lower-case letters, digits or hyphens, starting with a letter or digit.";
        }
        else if (string.Equals(Id, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            yield return $"Id must not be '{MultitenancyConstants.Root.Id}': that id is the operator tenant of a multi-tenant host.";
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return "Name is required.";
        }

        if (!CountryPattern().IsMatch(Country))
        {
            yield return "Country must be an ISO 3166-1 alpha-2 code such as 'SI'.";
        }

        if (string.IsNullOrWhiteSpace(AdminEmail) || !new EmailAddressAttribute().IsValid(AdminEmail))
        {
            yield return "AdminEmail must be a valid e-mail address.";
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z]{2}$")]
    private static partial Regex CountryPattern();
}
