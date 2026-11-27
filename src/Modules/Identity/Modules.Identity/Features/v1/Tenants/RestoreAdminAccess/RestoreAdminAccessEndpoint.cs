using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.v1.Tenants.RestoreAdminAccess;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Tenants.RestoreAdminAccess;

public static class RestoreAdminAccessEndpoint
{
    public sealed record Body(string Email, string Reason, string? FirstName = null, string? LastName = null);

    internal static RouteHandlerBuilder MapRestoreAdminAccessEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/tenants/{tenantId}/restore-admin",
            async (string tenantId, [FromBody] Body body, IMediator mediator, CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(
                    new RestoreAdminAccessCommand(tenantId, body.Email, body.Reason, body.FirstName, body.LastName), ct)))
            .WithName("RestoreAdminAccess")
            .WithSummary("Give a tenant an Admin again")
            .WithDescription("Platform operators only, after checking with the customer. An active member with the address becomes Admin, or the address is invited as Admin. Nothing else changes; the tenant's active Admins get an e-mail, and the tenant's audit trail records it.")
            .RequirePermission(IdentityPermissions.Users.RestoreAdmin)
            .Produces<RestoreAdminAccessResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}
