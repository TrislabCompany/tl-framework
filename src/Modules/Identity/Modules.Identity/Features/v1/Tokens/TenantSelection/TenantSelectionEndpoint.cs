using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSelection;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TenantSelection;

public static class TenantSelectionEndpoint
{
    internal static RouteHandlerBuilder MapTenantSelectionEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/select",
            [AllowAnonymous] async Task<Ok<SignInResponse>>
            ([FromBody] SelectTenantCommand command,
            [FromHeader(Name = GenerateTokenEndpoint.AppHeader)] string? app,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
                TypedResults.Ok(await mediator.Send(command with { App = app }, ct)))
            .WithName("SelectTenantAfterSignIn")
            .WithSummary("Choose the tenant after a sign-in")
            .WithDescription("Finishes a sign-in that answered tenantSelectionRequired: send its selectionToken and the chosen tenant to get the tokens for that tenant. setDefault opens the tenant directly at the next sign-in. The selection token is valid for 5 minutes and ends with a password change.")
            .Produces<SignInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
