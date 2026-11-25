using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Invitations.GetInvitations;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Invitations.GetInvitations;

public static class GetInvitationsEndpoint
{
    internal static RouteHandlerBuilder MapGetInvitationsEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/invitations", async ([AsParameters] GetInvitationsQuery query, IMediator mediator, CancellationToken ct) =>
            TypedResults.Ok(await mediator.Send(query, ct)))
            .WithName("GetInvitations")
            .WithSummary("List the tenant's invitations")
            .WithDescription("A page of the tenant's invitations, newest first: the address the administrator typed and its state (Pending, Accepted, Revoked or Expired), nothing about the account behind it.")
            .RequirePermission(IdentityPermissions.Users.View)
            .Produces<PagedResponse<InvitationDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
}
