using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Auditing.Contracts.Authorization;
using Trislab.Modules.Auditing.Contracts.Dtos;
using Trislab.Modules.Auditing.Contracts.v1.GetMyAudits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Auditing.Features.v1.GetMyAudits;

public static class GetMyAuditsEndpoint
{
    public static RouteHandlerBuilder MapGetMyAuditsEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/mine",
                async ([AsParameters] GetMyAuditsQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("GetMyAudits")
            .WithSummary("List the signed-in user's own activity")
            .WithDescription("The caller's audit events in their own tenant, with the filters of GetAudits. Needs only AuditTrails.ViewOwn, a Basic permission.")
            .RequirePermission(AuditingPermissions.AuditTrails.ViewOwn)
            .Produces<PagedResponse<AuditSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
