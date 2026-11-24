using System.Collections.ObjectModel;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Users.GetUserLookup;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Users.GetUserLookup;

public static class GetUserLookupEndpoint
{
    internal static RouteHandlerBuilder MapGetUserLookupEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet(
                "/users/lookup",
                async ([AsParameters] GetUserLookupQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .WithName("GetUserLookup")
            .WithSummary("Find users for a picker, or show users' names")
            .WithDescription("Returns the name and picture of active users, by search or by ids. Needs only Users.Pick, so a picker works for every user without the full user list.")
            .RequirePermission(IdentityPermissions.Users.Pick)
            .Produces<ReadOnlyCollection<UserLookupDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
