using Trislab.Framework.Core.Localization;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;

public static class GenerateTokenEndpoint
{
    /// <summary>
    /// Header used by clients to identify which app shell is requesting the token.
    /// SuperAdmin (root tenant) accounts are restricted to the admin app — submitting
    /// "dashboard" with tenant=root yields a 403 instead of a useful token. This is a
    /// belt-and-braces check; the dashboard client also rejects root-tenant tokens
    /// locally for a cleaner UX.
    /// </summary>
    public const string AppHeader = "X-Trislab-App";
    public const string AppAdmin = "admin";
    public const string AppDashboard = "dashboard";

    public static RouteHandlerBuilder MapGenerateTokenEndpoint(this IEndpointRouteBuilder endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint.MapPost("/token/issue",
            [AllowAnonymous] async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
            ([FromBody] GenerateTokenCommand command,
            [DefaultValue("root")][FromHeader] string? tenant,
            [FromHeader(Name = AppHeader)] string? app,
            [FromServices] IMediator mediator,
            [FromServices] IOptions<TenancyOptions> tenancy,
            [FromServices] IStringLocalizer<SharedResources> localizer,
            CancellationToken ct) =>
            {
                if (tenancy.Value.Mode == TenancyMode.Multitenant && IsRootViaDashboard(tenant, app))
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: localizer["Error.AppBoundary"],
                        detail: localizer["Error.AppBoundary.Detail"]);
                }

                var token = await mediator.Send(command, ct);
                return token is null
                    ? TypedResults.Unauthorized()
                    : TypedResults.Ok(token);
            })
            .RequireTenantHeaderWhenMultitenant()
            .WithName("IssueJwtTokens")
            .WithSummary("Issue JWT access and refresh tokens")
            .WithDescription("Submit credentials to receive a JWT access token and a refresh token. In multi-tenant mode provide the 'tenant' header to select the tenant context; in single-tenant mode send none, the server resolves the fixed tenant. The 'X-Trislab-App' header (admin|dashboard) is used to enforce the SuperAdmin / dashboard boundary.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static bool IsRootViaDashboard(string? tenant, string? app)
    {
        return string.Equals(tenant, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(app, AppDashboard, StringComparison.OrdinalIgnoreCase);
    }
}
