using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.GetImpersonationRequests;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.GetImpersonationRequests;

public static class GetImpersonationRequestsEndpoints
{
    internal static RouteHandlerBuilder MapGetMyImpersonationRequestsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/impersonation/requests/mine",
            async (IMediator mediator, CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(new GetMyImpersonationRequestsQuery(), ct)))
            .WithName("GetMyImpersonationRequests")
            .WithSummary("The requests to impersonate you")
            .WithDescription("Pending and approved requests to impersonate the signed-in user in the current tenant, and the used ones whose impersonation is still running, newest first.")
            .RequirePermission(IdentityPermissions.Impersonation.Respond)
            .Produces<IReadOnlyList<ImpersonationRequestDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    internal static RouteHandlerBuilder MapGetSentImpersonationRequestsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/impersonation/requests",
            async Task<Ok<IReadOnlyList<ImpersonationRequestDto>>> (
                [AsParameters] GetSentImpersonationRequestsQuery query, IMediator mediator, CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(query, ct)))
            .WithName("GetSentImpersonationRequests")
            .WithSummary("The impersonation requests you made")
            .WithDescription("The signed-in operator's requests with their answers, newest first; filter by status.")
            .RequirePermission(IdentityPermissions.Users.Impersonate)
            .Produces<IReadOnlyList<ImpersonationRequestDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
