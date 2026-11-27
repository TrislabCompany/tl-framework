using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Multitenancy;

/// <summary>
/// What a root operator may send while reading inside another tenant through the <c>tenant</c> header (D-44):
/// the safe methods, and the few POST endpoints that change no data. Routing runs before module middleware,
/// so the matched endpoint is known here.
/// </summary>
internal static class ReadOnlyRequest
{
    /// <summary>POST endpoints that change no tenant data: the SSE stream token.</summary>
    private static readonly HashSet<string> AllowedWrites = new(StringComparer.Ordinal) { "SseToken" };

    public static bool IsAllowed(HttpContext ctx)
    {
        var method = ctx.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return true;
        }

        var name = ctx.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        return name is not null && AllowedWrites.Contains(name);
    }
}
