using Trislab.Modules.Identity.Contracts.Authorization;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.RequestImpersonation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.RequestImpersonation;

public static class RequestImpersonationEndpoint
{
    internal static RouteHandlerBuilder MapRequestImpersonationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/impersonation/requests",
            async ([FromBody] RequestImpersonationCommand command,
                   IMediator mediator,
                   CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(command, ct)))
            .WithName("RequestImpersonation")
            .WithSummary("Ask a user for read-only access as that user")
            .WithDescription("Creates a pending request with a reason. The user approves or denies it; only an approved request starts an impersonation, once, within 24 hours. Platform operators (root tenant) may ask any user; tenant admins only users of their own tenant.")
            .RequirePermission(IdentityPermissions.Users.Impersonate)
            .Produces<ImpersonationRequestDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
}
