using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Contracts.v1.Users.ConfirmEmail;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Users.ConfirmEmail;

public static class ConfirmEmailEndpoint
{
    internal static RouteHandlerBuilder MapConfirmEmailEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/confirm-email", async (
            [FromBody] ConfirmEmailCommand command,
            [FromHeader(Name = MultitenancyConstants.Identifier)] string? tenant,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command with { Tenant = tenant ?? string.Empty }, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName("ConfirmEmail")
        .WithSummary("Confirm user email")
        .WithDescription("Confirm a user's email address with the code from the e-mail and the account's password.")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK);
    }
}