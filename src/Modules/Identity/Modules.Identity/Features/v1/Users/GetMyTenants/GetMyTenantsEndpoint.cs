using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Users.GetMyTenants;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Users.GetMyTenants;

public static class GetMyTenantsEndpoint
{
    internal static RouteHandlerBuilder MapGetMyTenantsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/profile/tenants", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new GetMyTenantsQuery(), cancellationToken)))
        .RequireAuthorization()
        .WithName("GetMyTenants")
        .WithSummary("Get the signed-in user's tenants")
        .WithDescription("The tenants the signed-in user is an active member of and can open now, with the default one marked, for the tenant menu. Switch with POST /token/switch.")
        .Produces<IReadOnlyList<TenantMembershipDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
