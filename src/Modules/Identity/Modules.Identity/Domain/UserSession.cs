using Trislab.Framework.Core.Domain;
using Trislab.Modules.Identity.Domain.Events;

namespace Trislab.Modules.Identity.Domain;

public class UserSession : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string RefreshTokenHash { get; private set; } = default!;
    public string IpAddress { get; private set; } = default!;
    public string UserAgent { get; private set; } = default!;
    public string? DeviceType { get; private set; }
    public string? Browser { get; private set; }
    public string? BrowserVersion { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? OsVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastActivityAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedBy { get; private set; }
    public string? RevokedReason { get; private set; }

    /// <summary>Short hash of the account's security stamp at sign-in. A password, 2FA or stamp change ends the session.</summary>
    public string? SecurityStampHash { get; private set; }

    /// <summary>The refresh token this session held before the last rotation, to detect reuse of an old token.</summary>
    public string? PreviousRefreshTokenHash { get; private set; }

    /// <summary>The app (X-Trislab-App) the session signed in through, when the client named one.</summary>
    public string? App { get; private set; }

    // Navigation property (init for EF Core materialization)
    public virtual TrislabUser? User { get; init; }

    // IHasDomainEvents implementation
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    private UserSession() { } // EF Core

    public static UserSession Create(
        string userId,
        string refreshTokenHash,
        string ipAddress,
        string userAgent,
        DateTime expiresAt,
        string? deviceType = null,
        string? browser = null,
        string? browserVersion = null,
        string? operatingSystem = null,
        string? osVersion = null,
        Guid? id = null)
    {
        return new UserSession
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            RefreshTokenHash = refreshTokenHash,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceType = deviceType,
            Browser = browser,
            BrowserVersion = browserVersion,
            OperatingSystem = operatingSystem,
            OsVersion = osVersion,
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime,
            LastActivityAt = TimeProvider.System.GetUtcNow().UtcDateTime,
            ExpiresAt = expiresAt
        };
    }

    public void UpdateActivity()
    {
        LastActivityAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>Binds the session to the account's security stamp and the app it signed in through.</summary>
    public void Bind(string? securityStampHash, string? app)
    {
        SecurityStampHash = securityStampHash;
        App = string.IsNullOrWhiteSpace(app) ? null : app.Trim().ToLowerInvariant();
    }

    public void UpdateRefreshToken(string refreshTokenHash, DateTime expiresAt)
    {
        PreviousRefreshTokenHash = RefreshTokenHash;
        RefreshTokenHash = refreshTokenHash;
        ExpiresAt = expiresAt;
        LastActivityAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    public void Revoke(string? revokedBy = null, string? reason = null, string? tenantId = null)
    {
        if (IsRevoked) return;
        IsRevoked = true;
        RevokedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        RevokedBy = revokedBy;
        RevokedReason = reason;

        AddDomainEvent(SessionRevokedEvent.Create(
            userId: UserId,
            sessionId: Id,
            revokedBy: revokedBy,
            reason: reason,
            tenantId: tenantId));
    }
}