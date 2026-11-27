using FluentValidation;
using Trislab.Framework.Core.Context;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationRequests;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.GetImpersonationRequests;

public sealed class GetMyImpersonationRequestsQueryHandler(
    IdentityDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IQueryHandler<GetMyImpersonationRequestsQuery, IReadOnlyList<ImpersonationRequestDto>>
{
    public async ValueTask<IReadOnlyList<ImpersonationRequestDto>> Handle(
        GetMyImpersonationRequestsQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = ImpersonationRequests.Caller(currentUser);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Open requests: waiting for an answer, or approved and not started yet.
        var open = await db.ImpersonationRequests
            .AsNoTracking()
            .Where(r => r.TargetUserId == userId && r.TargetTenantId == tenantId
                && (r.State == ImpersonationRequestState.Pending || r.State == ImpersonationRequestState.Approved)
                && r.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Used requests whose impersonation is still running, so the user can end it.
        var running = await db.ImpersonationRequests
            .AsNoTracking()
            .Where(r => r.TargetUserId == userId && r.TargetTenantId == tenantId
                && r.State == ImpersonationRequestState.Used
                && db.ImpersonationGrants.Any(g => g.Id == r.GrantId
                    && g.EndedAtUtc == null && g.RevokedAtUtc == null && g.ExpiresAtUtc > now))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return open.Concat(running)
            .OrderByDescending(r => r.RequestedAtUtc)
            .Select(r => ImpersonationRequests.ToDto(r, now))
            .ToList();
    }
}

public sealed class GetSentImpersonationRequestsQueryHandler(
    IdentityDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IQueryHandler<GetSentImpersonationRequestsQuery, IReadOnlyList<ImpersonationRequestDto>>
{
    public async ValueTask<IReadOnlyList<ImpersonationRequestDto>> Handle(
        GetSentImpersonationRequestsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (userId, tenantId) = ImpersonationRequests.Caller(currentUser);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var q = db.ImpersonationRequests
            .AsNoTracking()
            .Where(r => r.ActorUserId == userId && r.ActorTenantId == tenantId);

        if (request.Status is { } status)
        {
            q = status switch
            {
                ImpersonationRequestStatus.Pending =>
                    q.Where(r => r.State == ImpersonationRequestState.Pending && r.ExpiresAtUtc > now),
                ImpersonationRequestStatus.Approved =>
                    q.Where(r => r.State == ImpersonationRequestState.Approved && r.ExpiresAtUtc > now),
                ImpersonationRequestStatus.Denied =>
                    q.Where(r => r.State == ImpersonationRequestState.Denied),
                ImpersonationRequestStatus.Used =>
                    q.Where(r => r.State == ImpersonationRequestState.Used),
                ImpersonationRequestStatus.Expired =>
                    q.Where(r => (r.State == ImpersonationRequestState.Pending || r.State == ImpersonationRequestState.Approved)
                        && r.ExpiresAtUtc <= now),
                _ => q,
            };
        }

        var rows = await q
            .OrderByDescending(r => r.RequestedAtUtc)
            .Take(request.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => ImpersonationRequests.ToDto(r, now)).ToList();
    }
}

public sealed class GetSentImpersonationRequestsQueryValidator : AbstractValidator<GetSentImpersonationRequestsQuery>
{
    public GetSentImpersonationRequestsQueryValidator()
    {
        RuleFor(p => p.Take).InclusiveBetween(1, 500);
        RuleFor(p => p.Status).IsInEnum().When(p => p.Status.HasValue);
    }
}
