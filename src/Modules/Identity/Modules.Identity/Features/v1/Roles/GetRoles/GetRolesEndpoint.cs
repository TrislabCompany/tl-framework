using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Roles.GetRoles;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Roles.GetRoles;

public static class GetRolesEndpoint
{
    public static RouteHandlerBuilder MapGetRolesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/roles",
            async ([AsParameters] GetRolesQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(query, cancellationToken)))
        .WithName("ListRoles")
        .WithSummary("List roles (paged)")
        .RequirePermission(IdentityPermissions.Roles.View)
        .Produces<PagedResponse<RoleDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .WithDescription("Retrieve roles available for the current tenant. Pageable via PageNumber/PageSize; filterable via Search (case-insensitive substring against name + description).");
    }
}
