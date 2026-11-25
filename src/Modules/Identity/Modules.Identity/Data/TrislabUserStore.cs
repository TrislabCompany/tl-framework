using Trislab.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Data;

/// <summary>
/// The Identity user store. A user account is global, so a lookup by e-mail or user name searches every
/// account, not only the current tenant's members: e-mail and user name are unique across tenants, and
/// Identity's own uniqueness check (<c>RequireUniqueEmail</c>, the user name validator) goes through these
/// two methods. A lookup by id stays limited to the members of the current tenant.
/// </summary>
/// <remarks>
/// A caller of <c>FindByEmailAsync</c> or <c>FindByNameAsync</c> gets an account, not a member: check the
/// membership before acting in the tenant.
/// </remarks>
public sealed class TrislabUserStore(IdentityDbContext context, IdentityErrorDescriber? describer = null)
    : UserStore<TrislabUser, TrislabRole, IdentityDbContext, string, IdentityUserClaim<string>,
        IdentityUserRole<string>, IdentityUserLogin<string>, IdentityUserToken<string>, TrislabRoleClaim,
        IdentityUserPasskey<string>>(context, describer)
{
    // UserStore.FindByIdAsync uses DbSet.FindAsync, which skips query filters: it would return any account
    // by id, whatever its tenant. A query keeps the membership filter, so only the tenant's members are found.
    public override Task<TrislabUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public override Task<TrislabUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Context.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public override Task<TrislabUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Context.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .SingleOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
    }
}
