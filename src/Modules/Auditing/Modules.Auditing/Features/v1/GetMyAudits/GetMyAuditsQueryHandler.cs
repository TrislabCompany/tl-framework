using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Auditing.Contracts.Dtos;
using Trislab.Modules.Auditing.Contracts.v1.GetAudits;
using Trislab.Modules.Auditing.Contracts.v1.GetMyAudits;
using Mediator;

namespace Trislab.Modules.Auditing.Features.v1.GetMyAudits;

/// <summary>
/// Runs <see cref="GetAuditsQuery"/> pinned to the caller: their user id and their own tenant, so
/// the result is the list an Admin sees when filtering by that user, and nothing more.
/// </summary>
public sealed class GetMyAuditsQueryHandler(ICurrentUser currentUser, IMediator mediator)
    : IQueryHandler<GetMyAuditsQuery, PagedResponse<AuditSummaryDto>>
{
    public async ValueTask<PagedResponse<AuditSummaryDto>> Handle(GetMyAuditsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedException("no current user").Localized("Error.NoCurrentUser");
        }

        var audits = new GetAuditsQuery
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            Sort = query.Sort,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            TenantId = null,
            UserId = userId.ToString(),
            EventType = query.EventType,
            ExcludeEventType = query.ExcludeEventType,
            Severity = query.Severity,
            Tags = query.Tags,
            Source = query.Source,
            Search = query.Search,
        };

        return await mediator.Send(audits, cancellationToken).ConfigureAwait(false);
    }
}
