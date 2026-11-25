using System.Security.Claims;
using Trislab.Modules.Identity.Contracts.v1.Invitations.AcceptInvitation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Invitations.AcceptInvitation;

public static class AcceptInvitationEndpoint
{
    internal static RouteHandlerBuilder MapAcceptInvitationEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/invitations/accept",
            [AllowAnonymous] async ([FromBody] AcceptInvitationCommand command, ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
            {
                // The caller is whoever the access token names, never the body.
                var caller = user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
                return TypedResults.Ok(await mediator.Send(command with { CallerUserId = caller }, ct));
            })
            .WithName("AcceptInvitation")
            .WithSummary("Accept an invitation")
            .WithDescription("An account its owner has not proven yet (new, or e-mail unconfirmed) sends the password to set, which also confirms the e-mail; a confirmed account must be signed in (its token), and 401 with code Identity.InvitationSignInRequired asks the client to sign in first. The token works once. Rate limited.")
            .RequireRateLimiting("auth")
            .Produces<AcceptInvitationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status429TooManyRequests);
}
