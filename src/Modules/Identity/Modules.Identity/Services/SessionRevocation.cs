using Trislab.Modules.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Services;

/// <summary>Ends a user's sessions in one statement: in every tenant (a password change) or in one (a removed membership).</summary>
internal static class SessionRevocation
{
    public static Task<int> RevokeAsync(
        IdentityDbContext db, string userId, string? tenantId, string revokedBy, string reason, DateTime nowUtc, CancellationToken ct) =>
        db.UserSessions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == userId && !s.IsRevoked && (tenantId == null || EF.Property<string>(s, "TenantId") == tenantId))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.IsRevoked, true)
                .SetProperty(s => s.RevokedAt, nowUtc)
                .SetProperty(s => s.RevokedBy, revokedBy)
                .SetProperty(s => s.RevokedReason, reason), ct);
}
