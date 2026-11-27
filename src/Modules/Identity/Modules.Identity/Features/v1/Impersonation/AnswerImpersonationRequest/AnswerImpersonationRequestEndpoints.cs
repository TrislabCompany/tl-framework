using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.AnswerImpersonationRequest;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.AnswerImpersonationRequest;

public static class AnswerImpersonationRequestEndpoints
{
    internal static RouteHandlerBuilder MapApproveImpersonationRequestEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/requests/{id:guid}/approve",
            async (Guid id, IMediator mediator, CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(new ApproveImpersonationRequestCommand(id), ct)))
            .WithName("ApproveImpersonationRequest")
            .WithSummary("Approve a request to impersonate you")
            .WithDescription("The requested user lets the operator start one read-only impersonation within the request's lifetime. Only the requested user, in the request's tenant, can approve it.")
            .RequirePermission(IdentityPermissions.Impersonation.Respond)
            .Produces<ImpersonationRequestDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    internal static RouteHandlerBuilder MapDenyImpersonationRequestEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/requests/{id:guid}/deny",
            async (Guid id, IMediator mediator, CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(new DenyImpersonationRequestCommand(id), ct)))
            .WithName("DenyImpersonationRequest")
            .WithSummary("Deny a request to impersonate you, or take your approval back")
            .WithDescription("Denies a pending request, takes an approval back, or ends the impersonation the request started. Only the requested user, in the request's tenant, can do it.")
            .RequirePermission(IdentityPermissions.Impersonation.Respond)
            .Produces<ImpersonationRequestDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
