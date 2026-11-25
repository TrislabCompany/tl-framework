using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;

public static class GenerateTokenEndpoint
{
    /// <summary>
    /// Header used by clients to identify which app shell is requesting the token. The two apps
    /// have separate users: root-tenant (operator) accounts sign in only to the admin app, and
    /// tenant accounts only to the dashboard. Either mismatch yields a 403 instead of a token.
    /// The header is chosen by the client, so this is a belt-and-braces check on top of the
    /// root-only permissions that guard every operator endpoint; both clients also reject a
    /// token of the wrong kind locally for a cleaner UX.
    /// </summary>
    public const string AppHeader = "X-Trislab-App";
    public const string AppAdmin = "admin";
    public const string AppDashboard = "dashboard";

    public static RouteHandlerBuilder MapGenerateTokenEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/issue",
            [AllowAnonymous] async Task<Ok<SignInResponse>>
            ([FromBody] GenerateTokenCommand command,
            [FromHeader] string? tenant,
            [FromHeader(Name = AppHeader)] string? app,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
            {
                // The headers are the only source of these two: the body cannot set them.
                var response = await mediator.Send(command with { Tenant = tenant, App = app }, ct);
                return TypedResults.Ok(response);
            })
            .WithName("IssueJwtTokens")
            .WithSummary("Sign in and issue JWT access and refresh tokens")
            .WithDescription("Submit e-mail and password. No tenant is needed: the server opens the tenant the optional 'tenant' header names when the user is a member there, else the user's default tenant, else their only tenant. A user with several tenants and no default gets tenantSelectionRequired, the tenant list and a selectionToken to send with the choice to POST /token/select. An operator (a member of root) always signs in to root. The 'X-Trislab-App' header (admin|dashboard) enforces the app boundary: root accounts sign in only to admin, tenant accounts only to dashboard.")
            .Produces<SignInResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }
}
