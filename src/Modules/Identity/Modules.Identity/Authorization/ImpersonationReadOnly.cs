using Trislab.Framework.Shared.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Trislab.Modules.Identity.Authorization;

/// <summary>
/// The rule that impersonation only reads (D-44): a request carrying an impersonation token (<c>act_sub</c>) may use
/// the safe methods, end the impersonation, get an SSE stream token and connect to the realtime hub (whose calls
/// <see cref="ImpersonationHubFilter"/> checks); every other write is refused. Routing runs
/// before module middleware, so the matched endpoint is known here.
/// </summary>
internal static class ImpersonationReadOnly
{
    /// <summary>Endpoint names an impersonation token may still POST to; none of them changes tenant data.</summary>
    private static readonly HashSet<string> AllowedWrites = new(StringComparer.Ordinal) { "EndImpersonation", "SseToken" };

    public static bool IsRefused(HttpContext ctx)
    {
        if (ctx.User?.Identity?.IsAuthenticated != true
            || string.IsNullOrEmpty(ctx.User.FindFirst(ClaimConstants.ActorSubject)?.Value))
        {
            return false;
        }

        var method = ctx.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return false;
        }

        var endpoint = ctx.GetEndpoint();

        // The realtime hub's negotiate and transport requests are POSTs that carry no data change; what a hub
        // call may do is decided per method by ImpersonationHubFilter.
        if (endpoint?.Metadata.GetMetadata<HubMetadata>() is not null)
        {
            return false;
        }

        var name = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        return name is null || !AllowedWrites.Contains(name);
    }
}
