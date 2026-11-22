using System.ComponentModel.DataAnnotations;

namespace Trislab.Modules.Identity;

/// <summary>
/// TOTP settings (config section <c>"TwoFactorOptions"</c>). <see cref="Issuer"/> is the name an
/// authenticator app shows next to the account; changing it only affects new enrollments, existing
/// secrets keep working.
/// </summary>
public sealed class TwoFactorOptions
{
    [Required]
    public string Issuer { get; set; } = "FullStackHero";
}
