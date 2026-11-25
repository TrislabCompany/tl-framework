using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Modules.Identity.Contracts.v1.Invitations.RevokeInvitation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Invitations.RevokeInvitation;

public static class RevokeInvitationEndpoint
{
    internal static RouteHandlerBuilder MapRevokeInvitationEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete("/invitations/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new RevokeInvitationCommand(id), ct);
            return TypedResults.NoContent();
        })
            .WithName("RevokeInvitation")
            .WithSummary("Revoke an invitation")
            .WithDescription("The link stops working, and a membership nobody accepted is removed. An accepted invitation stays accepted: remove the user instead.")
            .RequirePermission(IdentityPermissions.Users.Create)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
}
