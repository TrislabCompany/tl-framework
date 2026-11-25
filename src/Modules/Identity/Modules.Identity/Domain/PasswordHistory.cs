using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.Identity.Domain;

/// <summary>Earlier password hashes of a (global) user account, so it is not tenant-scoped.</summary>
public class PasswordHistory : IGlobalEntity
{
    public int Id { get; init; }
    public string UserId { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    // Navigation property (init for EF Core materialization)
    public virtual TrislabUser? User { get; init; }

    private PasswordHistory() { } // EF Core

    public static PasswordHistory Create(string userId, string passwordHash)
    {
        return new PasswordHistory
        {
            UserId = userId,
            PasswordHash = passwordHash,
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
        };
    }
}