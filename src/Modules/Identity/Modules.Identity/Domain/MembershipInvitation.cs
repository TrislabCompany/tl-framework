namespace Trislab.Modules.Identity.Domain;

/// <summary>
/// An invitation to a pending <see cref="TenantMembership"/>, e-mailed to the account's owner. Only the
/// owner can accept it: the token is single-use, stored only as a hash, expires and can be revoked.
/// </summary>
/// <remarks>
/// Tenant-scoped. Accepting reads it across tenants by the token's hash, because the owner may be signed
/// in to another tenant or not at all.
/// </remarks>
public class MembershipInvitation
{
    public Guid Id { get; private set; }
    public Guid MembershipId { get; private set; }
    public string UserId { get; private set; } = default!;

    /// <summary>The address the invitation was sent to, as the administrator typed it.</summary>
    public string Email { get; private set; } = default!;

    /// <summary>SHA-256 of the token. The token itself is only in the e-mail.</summary>
    public string TokenHash { get; private set; } = default!;

    public string CreatedBy { get; private set; } = default!;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? AcceptedOnUtc { get; private set; }
    public DateTime? RevokedOnUtc { get; private set; }
    public string? RevokedBy { get; private set; }

    private MembershipInvitation() { } // EF Core

    public static MembershipInvitation Create(
        Guid membershipId, string userId, string email, string tokenHash, string createdBy, DateTime nowUtc, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "An invitation must expire in the future.");
        }

        return new MembershipInvitation
        {
            Id = Guid.NewGuid(),
            MembershipId = membershipId,
            UserId = userId,
            Email = email,
            TokenHash = tokenHash,
            CreatedBy = createdBy,
            CreatedOnUtc = nowUtc,
            ExpiresOnUtc = nowUtc.Add(lifetime),
        };
    }

    public InvitationStatus StatusAt(DateTime nowUtc)
    {
        if (AcceptedOnUtc is not null) return InvitationStatus.Accepted;
        if (RevokedOnUtc is not null) return InvitationStatus.Revoked;
        return nowUtc >= ExpiresOnUtc ? InvitationStatus.Expired : InvitationStatus.Pending;
    }

    public void MarkAccepted(DateTime nowUtc)
    {
        if (StatusAt(nowUtc) != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be accepted.");
        }

        AcceptedOnUtc = nowUtc;
    }

    /// <summary>Revokes a pending or expired invitation; an accepted one stays accepted.</summary>
    public bool Revoke(string revokedBy, DateTime nowUtc)
    {
        if (AcceptedOnUtc is not null || RevokedOnUtc is not null) return false;
        RevokedOnUtc = nowUtc;
        RevokedBy = revokedBy;
        return true;
    }
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}
