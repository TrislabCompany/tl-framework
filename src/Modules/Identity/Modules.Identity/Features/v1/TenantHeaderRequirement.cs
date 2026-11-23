using Trislab.Framework.Shared.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Trislab.Modules.Identity.Features.v1;

/// <summary>
/// The anonymous endpoints (sign-in, refresh, forgot and reset password, self-register) name the tenant in a
/// <c>tenant</c> header in multi-tenant mode. In single-tenant mode the server always resolves the fixed tenant and
/// the header is neither needed nor read, so a client sends none.
/// </summary>
internal static class TenantHeaderRequirement
{
    internal static RouteHandlerBuilder RequireTenantHeaderWhenMultitenant(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var mode = http.RequestServices.GetRequiredService<IOptions<TenancyOptions>>().Value.Mode;
            if (mode == TenancyMode.Multitenant
                && string.IsNullOrWhiteSpace(http.Request.Headers[MultitenancyConstants.Identifier].ToString()))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: $"Required header '{MultitenancyConstants.Identifier}' was not provided.");
            }

            return await next(context).ConfigureAwait(false);
        });
    }
}
