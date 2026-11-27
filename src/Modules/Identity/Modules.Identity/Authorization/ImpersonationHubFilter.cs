using Trislab.Framework.Shared.Constants;
using Microsoft.AspNetCore.SignalR;

namespace Trislab.Modules.Identity.Authorization;

/// <summary>
/// Impersonation only reads over the realtime hub too (D-44): an <c>act_sub</c> connection may subscribe, but every
/// other hub method (typing as the user, and any method added later) is refused. Refusing by default keeps a new
/// hub method from becoming a way to act as the user.
/// </summary>
internal sealed class ImpersonationHubFilter : IHubFilter
{
    /// <summary>Hub methods that only subscribe the connection to what the user may already read.</summary>
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.Ordinal) { "JoinChannel" };

    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        var impersonating = !string.IsNullOrEmpty(
            invocationContext.Context.User?.FindFirst(ClaimConstants.ActorSubject)?.Value);
        if (impersonating && !AllowedMethods.Contains(invocationContext.HubMethodName))
        {
            throw new HubException("Impersonation only reads.");
        }

        return next(invocationContext);
    }
}
