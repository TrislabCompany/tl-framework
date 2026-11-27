using FluentValidation;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.AnswerImpersonationRequest;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Identity.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.AnswerImpersonationRequest;

/// <summary>
/// Finds a request addressed to the signed-in user in the current tenant. Anyone else's request is a 404,
/// so another user, an operator or another tenant can't answer it, or learn that it exists.
/// </summary>
internal static class MyRequest
{
    public static async Task<ImpersonationRequest> FindAsync(
        IdentityDbContext db, ICurrentUser currentUser, Guid requestId, CancellationToken ct)
    {
        var (userId, tenantId) = ImpersonationRequests.Caller(currentUser);
        return await db.ImpersonationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TargetUserId == userId && r.TargetTenantId == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw ImpersonationRequests.NotFound();
    }

    /// <summary>The row as it is now, after a conditional update.</summary>
    public static Task<ImpersonationRequest> ReloadAsync(IdentityDbContext db, Guid requestId, CancellationToken ct) =>
        db.ImpersonationRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct);
}

public sealed class ApproveImpersonationRequestCommandHandler(
    IdentityDbContext db,
    ICurrentUser currentUser,
    IdentityAuditor auditor,
    TimeProvider timeProvider)
    : ICommandHandler<ApproveImpersonationRequestCommand, ImpersonationRequestDto>
{
    public async ValueTask<ImpersonationRequestDto> Handle(
        ApproveImpersonationRequestCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await MyRequest.FindAsync(db, currentUser, request.RequestId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Conditional: only a pending, unexpired request turns approved, so a concurrent deny always wins.
        var approved = await db.ImpersonationRequests
            .Where(r => r.Id == entity.Id && r.State == ImpersonationRequestState.Pending && r.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, ImpersonationRequestState.Approved)
                .SetProperty(r => r.AnsweredAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        if (approved == 0)
        {
            throw new CustomException("only a pending request can be approved", errors: null, System.Net.HttpStatusCode.Conflict)
                .Localized<IdentityResources>("Identity.ImpersonationRequestNotPending");
        }

        await auditor.WriteAsync(
            SecurityAction.ImpersonationApproved,
            entity.TargetUserId,
            "ImpersonationApproved",
            new Dictionary<string, object?>
            {
                ["requestId"] = entity.Id,
                ["actorUserId"] = entity.ActorUserId,
                ["actorTenant"] = entity.ActorTenantId,
            },
            ct: cancellationToken).ConfigureAwait(false);

        var current = await MyRequest.ReloadAsync(db, entity.Id, cancellationToken).ConfigureAwait(false);
        return ImpersonationRequests.ToDto(current, now);
    }
}

public sealed class DenyImpersonationRequestCommandHandler(
    IdentityDbContext db,
    ICurrentUser currentUser,
    IImpersonationGrantService grantService,
    IdentityAuditor auditor,
    TimeProvider timeProvider)
    : ICommandHandler<DenyImpersonationRequestCommand, ImpersonationRequestDto>
{
    public async ValueTask<ImpersonationRequestDto> Handle(
        DenyImpersonationRequestCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await MyRequest.FindAsync(db, currentUser, request.RequestId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Conditional, whatever state it is in now: a concurrent start can't turn it back into "used".
        var denied = await db.ImpersonationRequests
            .Where(r => r.Id == entity.Id && r.State != ImpersonationRequestState.Denied)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, ImpersonationRequestState.Denied)
                .SetProperty(r => r.AnsweredAtUtc, now), cancellationToken)
            .ConfigureAwait(false);

        // Read the grant after the update: a start that linked it before is revoked here; one that links it
        // later sees the denial and revokes it itself (StartImpersonationCommandHandler).
        var current = await MyRequest.ReloadAsync(db, entity.Id, cancellationToken).ConfigureAwait(false);
        if (current.GrantId is { } grantId)
        {
            await grantService.RevokeAsync(
                grantId,
                revokedByUserId: current.TargetUserId,
                revokedByUserName: currentUser.Name,
                reason: "Taken back by the impersonated user",
                ct: cancellationToken).ConfigureAwait(false);
        }

        if (denied > 0)
        {
            await auditor.WriteAsync(
                SecurityAction.ImpersonationDenied,
                current.TargetUserId,
                "ImpersonationDenied",
                new Dictionary<string, object?>
                {
                    ["requestId"] = current.Id,
                    ["actorUserId"] = current.ActorUserId,
                    ["actorTenant"] = current.ActorTenantId,
                    ["grantId"] = current.GrantId,
                },
                ct: cancellationToken).ConfigureAwait(false);
        }

        return ImpersonationRequests.ToDto(current, now);
    }
}

public sealed class ApproveImpersonationRequestCommandValidator : AbstractValidator<ApproveImpersonationRequestCommand>
{
    public ApproveImpersonationRequestCommandValidator()
    {
        RuleFor(p => p.RequestId).NotEmpty();
    }
}

public sealed class DenyImpersonationRequestCommandValidator : AbstractValidator<DenyImpersonationRequestCommand>
{
    public DenyImpersonationRequestCommandValidator()
    {
        RuleFor(p => p.RequestId).NotEmpty();
    }
}
