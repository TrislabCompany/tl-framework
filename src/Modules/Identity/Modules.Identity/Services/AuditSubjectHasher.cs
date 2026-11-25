using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Trislab.Modules.Identity.Authorization.Jwt;

namespace Trislab.Modules.Identity.Services;

/// <summary>
/// Names an e-mail address in the audit trail without writing it there: a keyed hash (HMAC-SHA-256) links the
/// repeated failed sign-ins of one address, but someone who reads the trail can't reverse it with a list of
/// addresses, because the key never leaves the server. The key is derived from the JWT signing key (HKDF with its
/// own label), so a host needs no extra secret; rotating the signing key starts new hashes.
/// </summary>
public sealed class AuditSubjectHasher
{
    private static readonly byte[] Label = Encoding.UTF8.GetBytes("Trislab.Identity.AuditSubject.v1");

    private readonly byte[] _key;

    public AuditSubjectHasher(IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);
        var signingKey = jwtOptions.Value.SigningKey;
        if (string.IsNullOrEmpty(signingKey))
        {
            throw new InvalidOperationException("JwtOptions:SigningKey is required to hash audit subjects.");
        }

        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(signingKey), 32, salt: null, info: Label);
    }

    /// <summary>The audit subject of an e-mail address: <c>email:</c> and 16 hex characters of its keyed hash.</summary>
    public string Email(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant()));
        return "email:" + Convert.ToHexString(mac.AsSpan(0, 8));
    }
}
