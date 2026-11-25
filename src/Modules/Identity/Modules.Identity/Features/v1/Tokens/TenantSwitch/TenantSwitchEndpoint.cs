using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSwitch;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TenantSwitch;

public static class TenantSwitchEndpoint
{
    internal static RouteHandlerBuilder MapTenantSwitchEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/switch",
            async Task<Ok<SignInResponse>>
            ([FromBody] SwitchTenantCommand command,
            [FromHeader(Name = GenerateTokenEndpoint.AppHeader)] string? app,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(command with { App = app }, ct)))
            .RequireAuthorization()
            .WithName("SwitchTenant")
            .WithSummary("Switch to another of the user's tenants")
            .WithDescription("Issues tokens for another tenant the signed-in user is an active member of, for the tenant menu. setDefault also makes it the tenant sign-in opens. The switch is audited. Not allowed while impersonating.")
            .Produces<SignInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
