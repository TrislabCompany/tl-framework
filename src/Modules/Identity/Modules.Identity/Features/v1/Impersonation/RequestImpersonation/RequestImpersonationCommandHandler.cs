using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.RequestImpersonation;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Identity.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.RequestImpersonation;

public sealed partial class RequestImpersonationCommandHandler(
    IdentityDbContext db,
    IIdentityService identityService,
    ICurrentUser currentUser,
    IdentityAuditor auditor,
    TimeProvider timeProvider,
    ILogger<RequestImpersonationCommandHandler> logger)
    : ICommandHandler<RequestImpersonationCommand, ImpersonationRequestDto>
{
    public async ValueTask<ImpersonationRequestDto> Handle(
        RequestImpersonationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (actorUserId, actorTenantId) = ImpersonationRequests.Caller(currentUser);

        // Cross-tenant impersonation requires the actor to be in the root tenant. Tenant admins
        // can only impersonate users within their own tenant.
        if (!string.Equals(actorTenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal)
            && !string.Equals(actorTenantId, request.TargetTenantId, StringComparison.Ordinal))
        {
            throw new ForbiddenException("cross-tenant impersonation is restricted to platform operators")
                .Localized<IdentityResources>("Identity.CrossTenantImpersonationRestricted");
        }

        if (string.Equals(actorUserId, request.TargetUserId, StringComparison.Ordinal)
            && string.Equals(actorTenantId, request.TargetTenantId, StringComparison.Ordinal))
        {
            throw new CustomException("cannot impersonate yourself", errors: null, System.Net.HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.CannotImpersonateYourself");
        }

        // The target must be an active member of that tenant; the same check the start makes again.
        var target = await identityService
            .BuildClaimsForUserAsync(request.TargetUserId, request.TargetTenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("target user not found")
                .Localized<IdentityResources>("Identity.TargetUserNotFound");

        var (targetSubject, targetClaims) = target;
        var targetUserName = targetClaims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
            ?? targetClaims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // One open request per operator and user: the user answers it, or it expires, before the next.
        var open = await db.ImpersonationRequests
            .AnyAsync(r => r.ActorUserId == actorUserId && r.ActorTenantId == actorTenantId
                && r.TargetUserId == targetSubject && r.TargetTenantId == request.TargetTenantId
                && (r.State == ImpersonationRequestState.Pending || r.State == ImpersonationRequestState.Approved)
                && r.ExpiresAtUtc > now, cancellationToken)
            .ConfigureAwait(false);
        if (open)
        {
            throw new CustomException("an open request for this user exists", errors: null, System.Net.HttpStatusCode.Conflict)
                .Localized<IdentityResources>("Identity.ImpersonationRequestOpen");
        }

        var entity = ImpersonationRequest.Create(
            actorUserId, currentUser.Name, actorTenantId,
            targetSubject, targetUserName, request.TargetTenantId,
            request.Reason.Trim(), now);

        db.ImpersonationRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditor.WriteAsync(
            SecurityAction.ImpersonationRequested,
            actorUserId,
            "ImpersonationRequested",
            new Dictionary<string, object?>
            {
                ["requestId"] = entity.Id,
                ["targetUserId"] = entity.TargetUserId,
                ["targetTenant"] = entity.TargetTenantId,
                ["reason"] = entity.Reason,
            },
            ct: cancellationToken).ConfigureAwait(false);

        LogRequested(logger, entity.Id, actorUserId, actorTenantId, entity.TargetUserId, entity.TargetTenantId);

        return ImpersonationRequests.ToDto(entity, now);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Impersonation requested: request {RequestId}, actor {ActorUserId}@{ActorTenant} -> target {TargetUserId}@{TargetTenant}")]
    private static partial void LogRequested(
        ILogger logger, Guid requestId, string actorUserId, string actorTenant, string targetUserId, string targetTenant);
}
