using System.Collections.ObjectModel;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Mailing;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Tenants.RestoreAdminAccess;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Identity.Services;
using Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Identity.Features.v1.Tenants.RestoreAdminAccess;

/// <summary>
/// The operator's "restore admin access" (D-44). Runs in the target tenant's own scope, so the tenant filters
/// isolate roles and memberships, and changes one thing: the Admin role of one address.
/// </summary>
public sealed partial class RestoreAdminAccessCommandHandler(
    ICurrentUser currentUser,
    IMultiTenantStore<AppTenantInfo, string> tenantStore,
    IEventTenantScope tenantScope,
    IServiceScopeFactory scopeFactory,
    IBackgroundMailer mailer,
    ILogger<RestoreAdminAccessCommandHandler> logger)
    : ICommandHandler<RestoreAdminAccessCommand, RestoreAdminAccessResponse>
{
    public async ValueTask<RestoreAdminAccessResponse> Handle(
        RestoreAdminAccessCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        // Root only: the permission is root-only already; the check keeps it so if a role is ever misconfigured.
        if (!string.Equals(currentUser.GetTenant(), MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            throw new ForbiddenException("restoring admin access is for platform operators")
                .Localized<IdentityResources>("Identity.RestoreAdminRootOnly");
        }

        // The root tenant's operators manage themselves; this is for customers' tenants.
        var tenant = string.Equals(request.TenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal)
            ? null
            : await tenantStore.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            throw new NotFoundException("tenant not found")
                .Localized<IdentityResources>("Identity.RestoreAdminTenantNotFound");
        }

        var operatorId = currentUser.GetUserId().ToString();
        var email = request.Email.Trim();
        RestoreAdminAccessOutcome outcome = default;
        List<string> notified = [];

        await tenantScope.ExecuteAsync(tenant.Id, async ct =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<IdentityDbContext>();
            var users = services.GetRequiredService<UserManager<TrislabUser>>();

            // The Admins to tell, read before the change so the new Admin isn't among them.
            var adminIds = (await users.GetUsersInRoleAsync(RoleConstants.Admin).ConfigureAwait(false))
                .Select(u => u.Id).ToList();
            notified = await db.ActiveMembers
                .AsNoTracking()
                .Where(u => adminIds.Contains(u.Id) && u.Email != null)
                .Select(u => u.Email!)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var account = await users.FindByEmailAsync(users.NormalizeEmail(email)).ConfigureAwait(false);
            var isMember = account is not null
                && await db.ActiveMembers.AnyAsync(u => u.Id == account.Id, ct).ConfigureAwait(false);

            if (isMember)
            {
                if (!await users.IsInRoleAsync(account!, RoleConstants.Admin).ConfigureAwait(false))
                {
                    await users.AddToRoleAsync(account!, RoleConstants.Admin).ConfigureAwait(false);
                    await services.GetRequiredService<IUserPermissionService>()
                        .InvalidatePermissionCacheAsync(account!.Id, ct).ConfigureAwait(false);
                }

                outcome = RestoreAdminAccessOutcome.RoleAssigned;
            }
            else
            {
                await services.GetRequiredService<MembershipInvitationService>()
                    .InviteAsync(email, request.FirstName, request.LastName, operatorId, asAdmin: true, ct)
                    .ConfigureAwait(false);
                outcome = RestoreAdminAccessOutcome.Invited;
            }

            // In the tenant's own audit trail, so its Admins see who restored access and why.
            await services.GetRequiredService<IdentityAuditor>().WriteAsync(
                SecurityAction.AdminAccessRestored,
                account?.Id,
                "AdminAccessRestored",
                new Dictionary<string, object?>
                {
                    ["operatorId"] = operatorId,
                    ["operatorTenant"] = MultitenancyConstants.Root.Id,
                    ["email"] = email,
                    ["outcome"] = outcome.ToString(),
                    ["reason"] = request.Reason,
                },
                AuditSeverity.Warning,
                ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        foreach (var to in notified.Where(a => !string.Equals(a, email, StringComparison.OrdinalIgnoreCase)))
        {
            mailer.Enqueue(Notice(to, tenant.Name ?? tenant.Id, email, outcome, request.Reason));
        }

        LogRestored(logger, tenant.Id, operatorId, outcome, notified.Count);
        return new RestoreAdminAccessResponse(outcome, notified.Count);
    }

    private static MailRequest Notice(
        string to, string tenantName, string email, RestoreAdminAccessOutcome outcome, string reason)
    {
        var what = outcome == RestoreAdminAccessOutcome.RoleAssigned
            ? $"{email} is now an administrator of {tenantName}."
            : $"{email} was invited to {tenantName} as an administrator.";
        var intro = $"A platform operator restored admin access to {tenantName}: {what} Reason: {reason}. "
            + "If you did not expect this, contact support and review the members and their roles.";

        return new MailRequest(
            new Collection<string> { to },
            $"Admin access restored in {tenantName}",
            HtmlEmail.Notice(heading: $"Admin access restored in {tenantName}", message: intro),
            textBody: intro);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Admin access restored in tenant {TenantId} by operator {OperatorId}: {Outcome}, {AdminsNotified} admins notified")]
    private static partial void LogRestored(
        ILogger logger, string tenantId, string operatorId, RestoreAdminAccessOutcome outcome, int adminsNotified);
}
