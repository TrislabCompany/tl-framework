using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Trislab.Framework.Web.BotProtection;

/// <summary>
/// Marks an endpoint as a public form that requires a Turnstile token. Applied by
/// <c>RequireTurnstile()</c>, so the wiring can be asserted from the endpoint map.
/// </summary>
public sealed class TurnstileProtectedEndpointMetadata
{
    public static TurnstileProtectedEndpointMetadata Instance { get; } = new();
}

/// <summary>
/// Refuses a request without a valid Turnstile token before the handler runs: 400 without a
/// token, 403 when Cloudflare refuses it, 503 when the check can't be made (no secret, or
/// siteverify unreachable). It never lets a request through unchecked.
/// </summary>
public sealed class TurnstileEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        var options = httpContext.RequestServices.GetRequiredService<IOptions<TurnstileOptions>>().Value;
        var token = httpContext.Request.Headers[options.HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(token))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "Bot check required",
                $"This form needs a Turnstile token in the {options.HeaderName} header.");
        }

        var verifier = httpContext.RequestServices.GetRequiredService<ITurnstileVerifier>();
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var result = await verifier.VerifyAsync(token, remoteIp, httpContext.RequestAborted).ConfigureAwait(false);

        return result switch
        {
            TurnstileResult.Passed => await next(context).ConfigureAwait(false),
            TurnstileResult.Failed => Problem(httpContext, StatusCodes.Status403Forbidden, "Bot check failed",
                "The bot check failed or expired. Complete it again and resend the form."),
            _ => Problem(httpContext, StatusCodes.Status503ServiceUnavailable, "Bot check unavailable",
                "The bot check can't be made right now. Try again later."),
        };
    }

    private static ProblemHttpResult Problem(HttpContext httpContext, int status, string title, string detail) =>
        TypedResults.Problem(detail: detail, instance: httpContext.Request.Path, statusCode: status, title: title);
}

public static class TurnstileExtensions
{
    /// <summary>
    /// Registers the Turnstile check: its options (section <c>TurnstileOptions</c>), the
    /// siteverify client and the verifier. Endpoints opt in with <c>RequireTurnstile()</c>.
    /// </summary>
    public static IServiceCollection AddTrislabTurnstile(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TurnstileOptions>()
            .BindConfiguration(nameof(TurnstileOptions))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HeaderName), "TurnstileOptions.HeaderName is required.")
            .Validate(o => o.VerifyUrl.IsAbsoluteUri, "TurnstileOptions.VerifyUrl must be an absolute URL.")
            .Validate(o => o.Timeout > TimeSpan.Zero, "TurnstileOptions.Timeout must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<ITurnstileVerifier, TurnstileVerifier>(TurnstileVerifier.HttpClientName)
            .ConfigureHttpClient((sp, client) => client.Timeout = sp.GetRequiredService<IOptions<TurnstileOptions>>().Value.Timeout);

        return services;
    }

    /// <summary>
    /// Makes this endpoint a public form: a request without a valid Turnstile token in the
    /// <c>X-Turnstile-Token</c> header is refused before the handler runs.
    /// </summary>
    public static RouteHandlerBuilder RequireTurnstile(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(TurnstileProtectedEndpointMetadata.Instance)
            .AddEndpointFilter<TurnstileEndpointFilter>();
    }
}
