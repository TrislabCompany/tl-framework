using Trislab.Framework.Core.Localization;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Localization;
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
            [AllowAnonymous] async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
            ([FromBody] GenerateTokenCommand command,
            [DefaultValue("root")][FromHeader] string? tenant,
            [FromHeader(Name = AppHeader)] string? app,
            [FromServices] IMediator mediator,
            [FromServices] IOptions<TenancyOptions> tenancy,
            [FromServices] IStringLocalizer<SharedResources> localizer,
            [FromServices] IStringLocalizer<IdentityResources> identityLocalizer,
            CancellationToken ct) =>
            {
                if (tenancy.Value.Mode == TenancyMode.Multitenant && IsRootViaDashboard(tenant, app))
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: localizer["Error.AppBoundary"],
                        detail: localizer["Error.AppBoundary.Detail"]);
                }

                if (tenancy.Value.Mode == TenancyMode.Multitenant && IsTenantViaAdmin(tenant, app))
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: identityLocalizer["Identity.OperatorAppBoundary"],
                        detail: identityLocalizer["Identity.OperatorAppBoundary.Detail"]);
                }

                var token = await mediator.Send(command, ct);
                return token is null
                    ? TypedResults.Unauthorized()
                    : TypedResults.Ok(token);
            })
            .RequireTenantHeaderWhenMultitenant()
            .WithName("IssueJwtTokens")
            .WithSummary("Issue JWT access and refresh tokens")
            .WithDescription("Submit credentials to receive a JWT access token and a refresh token. In multi-tenant mode provide the 'tenant' header to select the tenant context; in single-tenant mode send none, the server resolves the fixed tenant. The 'X-Trislab-App' header (admin|dashboard) enforces the app boundary: root-tenant accounts sign in only to admin, tenant accounts only to dashboard.")
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

    private static bool IsTenantViaAdmin(string? tenant, string? app)
    {
        return string.Equals(app, AppAdmin, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(tenant, MultitenancyConstants.Root.Id, StringComparison.OrdinalIgnoreCase);
    }
}
