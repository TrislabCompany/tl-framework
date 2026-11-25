using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Trislab.Modules.Identity.Services;

/// <summary>What a sign-in's selection token carries.</summary>
/// <param name="Id">The id of its server-side row; deleting the row uses the token up.</param>
/// <param name="UserId">The signed-in account.</param>
/// <param name="StampHash">The account's security-stamp hash at sign-in; a later change cancels the token.</param>
/// <param name="ExpiresOnUtc">When it stops working.</param>
public sealed record SelectionTokenPayload(Guid Id, string UserId, string StampHash, DateTime ExpiresOnUtc);

/// <summary>
/// Seals and opens sign-in selection tokens with Data Protection under their own purpose. A token that was
/// changed, sealed for another purpose or past its expiry reads as null. That it works only once is the
/// server-side row's job (<see cref="Domain.SignInSelection"/>).
/// </summary>
public sealed class SelectionTokenCodec(IDataProtectionProvider dataProtection, TimeProvider timeProvider)
{
    /// <summary>How long a selection token works.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    internal const string Purpose = "Trislab.Identity.TenantSelection.v2";

    private IDataProtector Protector => dataProtection.CreateProtector(Purpose);

    public string Protect(SelectionTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return Protector.Protect(string.Join(
            '\n',
            payload.Id.ToString("N"),
            payload.UserId,
            payload.StampHash,
            payload.ExpiresOnUtc.Ticks.ToString(CultureInfo.InvariantCulture)));
    }

    public SelectionTokenPayload? Read(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string plain;
        try
        {
            plain = Protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        var parts = plain.Split('\n');
        if (parts.Length != 4
            || !Guid.TryParseExact(parts[0], "N", out var id)
            || !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            return null;
        }

        var expires = new DateTime(ticks, DateTimeKind.Utc);
        return timeProvider.GetUtcNow().UtcDateTime >= expires
            ? null
            : new SelectionTokenPayload(id, parts[1], parts[2], expires);
    }
}
