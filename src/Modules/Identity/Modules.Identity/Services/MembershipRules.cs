using System.Net;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Services;

/// <summary>The rules every way into a tenant follows: invitation, self-registration and seeding.</summary>
internal static class MembershipRules
{
    /// <summary>
    /// Operators (members of the root tenant) stay in root, and a tenant's account never becomes an operator:
    /// an account with a membership in root can't join another tenant, nor an account with another tenant join root.
    /// </summary>
    public static async Task EnsureOperatorsStayInRootAsync(IdentityDbContext db, string userId, string tenantId, CancellationToken ct)
    {
        var otherTenants = await db.TenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.TenantId != tenantId)
            .Select(m => m.TenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (IsRootRuleBroken(tenantId, otherTenants))
        {
            throw new CustomException(
                "Operator accounts belong to the root tenant only.",
                errors: null,
                HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.OperatorAccountsStayInRoot");
        }
    }

    /// <summary>True when joining <paramref name="tenantId"/> would mix root with another tenant.</summary>
    public static bool IsRootRuleBroken(string tenantId, IReadOnlyCollection<string> otherTenants)
    {
        var root = MultitenancyConstants.Root.Id;
        return otherTenants.Count > 0
            && (string.Equals(tenantId, root, StringComparison.Ordinal) || otherTenants.Contains(root));
    }

    /// <summary>Gives a new member the Basic role and the tenant's default groups. Runs in the member's tenant.</summary>
    public static async Task AssignDefaultRoleAndGroupsAsync(
        UserManager<TrislabUser> userManager, IdentityDbContext db, TrislabUser user, string source, CancellationToken ct)
    {
        if (!await userManager.IsInRoleAsync(user, RoleConstants.Basic).ConfigureAwait(false))
        {
            await userManager.AddToRoleAsync(user, RoleConstants.Basic).ConfigureAwait(false);
        }

        var defaultGroups = await db.Groups
            .AsNoTracking()
            .Where(g => g.IsDefault && !g.IsDeleted)
            .Select(g => g.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existing = await db.UserGroups
            .Where(ug => ug.UserId == user.Id)
            .Select(ug => ug.GroupId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var added = false;
        foreach (var groupId in defaultGroups.Except(existing))
        {
            db.UserGroups.Add(UserGroup.Create(user.Id, groupId, source));
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
