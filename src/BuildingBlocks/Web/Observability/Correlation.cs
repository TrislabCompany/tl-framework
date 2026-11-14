using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FSH.Framework.Web.Observability;

/// <summary>
/// The correlation id of the platform's log contract (ADR 0062 in
/// tl-cloud-infrastructure) is the request's W3C trace id: taken from the
/// caller's traceparent header, or new. Every log line carries it as
/// trace_id, every response as X-Correlation-ID, and every ProblemDetails
/// response as traceId, so the id a user quotes to support finds the request.
/// </summary>
public static class Correlation
{
    /// <summary>The response header that carries the correlation id.</summary>
    public const string Header = "X-Correlation-ID";

    /// <summary>The trace id of the current request, or the request's own id when there is no activity.</summary>
    public static string Id(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
    }

    /// <summary>
    /// Sets X-Correlation-ID on every response. Add it before the exception
    /// handler: the header is set when the response starts, so it survives the
    /// handler clearing the response.
    /// </summary>
    public static IApplicationBuilder UseCorrelationHeader(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (context, next) =>
        {
            var id = Id(context);
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[Header] = id;
                return Task.CompletedTask;
            });
            await next(context).ConfigureAwait(false);
        });
    }
}
