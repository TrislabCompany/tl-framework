using System.Security.Claims;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Identity.Claims;
using Trislab.Modules.Notifications.Contracts.Events;

namespace TestHost;

internal static class TestEndpoints
{
    /// <summary>POST /channels creates a channel with the caller in it; POST /channels/{id}/members adds users.</summary>
    public static RouteGroupBuilder MapTestChannelEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/channels", (ClaimsPrincipal user, TestChannelMembership membership)
            => Results.Ok(membership.CreateChannel(user.GetUserId() ?? string.Empty)));

        group.MapPost("/channels/{channelId:guid}/members", (Guid channelId, AddMembersRequest request, TestChannelMembership membership)
            => request.UserIds.All(userId => membership.Add(channelId, userId)) ? Results.NoContent() : Results.NotFound());

        return group;
    }

    /// <summary>
    /// POST /notifications asks the Notifications module, over the event bus, to put one row in a user's inbox and
    /// push it to the user's open connections, the way any module raises a mention.
    /// It publishes on the bus, not the outbox, the way a mention does: the test reads the row right after the call.
    /// A product's own events belong on the outbox.
    /// </summary>
    public static RouteGroupBuilder MapTestNotificationEndpoint(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/notifications", async (NotifyRequest request, ClaimsPrincipal user, IEventBus eventBus, CancellationToken ct) =>
        {
            await eventBus.PublishAsync(
                new UserNotificationRequestedIntegrationEvent(
                    Id: Guid.NewGuid(),
                    OccurredOnUtc: DateTime.UtcNow,
                    TenantId: user.GetTenant(),
                    CorrelationId: Guid.NewGuid().ToString(),
                    Source: "TestHost",
                    UserId: request.UserId,
                    Type: request.Type,
                    Title: request.Title,
                    Body: request.Body,
                    Link: request.Link,
                    Metadata: null),
                ct);
            return Results.NoContent();
        });

        return group;
    }

    internal sealed record AddMembersRequest(IReadOnlyList<string> UserIds);

    internal sealed record NotifyRequest(string UserId, string Type, string Title, string? Body, string? Link);
}
