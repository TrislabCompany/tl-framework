using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Invitations.InviteUser;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Invitations.InviteUser;

public static class InviteUserEndpoint
{
    internal static RouteHandlerBuilder MapInviteUserEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/invitations", async ([FromBody] InviteUserCommand command, IMediator mediator, CancellationToken ct) =>
            TypedResults.Accepted((string?)null, await mediator.Send(command, ct)))
            .WithName("InviteUser")
            .WithSummary("Invite a user to the tenant")
            .WithDescription("The only way an administrator adds a user. E-mails an invitation; the membership stays pending, and the user invisible to the tenant, until the account's owner accepts. A new address gets an account without a password, which its owner chooses when accepting. The answer is the same whether the address has an account. Rate limited.")
            .RequirePermission(IdentityPermissions.Users.Create)
            .RequireRateLimiting("auth")
            .Produces<InvitationDto>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);
}
