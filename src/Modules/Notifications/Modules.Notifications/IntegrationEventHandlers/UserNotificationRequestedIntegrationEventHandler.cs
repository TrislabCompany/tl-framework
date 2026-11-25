using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Web.Realtime;
using Trislab.Modules.Notifications.Contracts.Events;
using Trislab.Modules.Notifications.Data;
using Trislab.Modules.Notifications.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Notifications.IntegrationEventHandlers;

/// <summary>
/// Handles <see cref="UserNotificationRequestedIntegrationEvent"/>, which any module may publish (Chat
/// does for a mention). Writes a row to the user's inbox and pushes a <c>NotificationCreated</c> event
/// to the user's SignalR group so the bell badge updates live.
///
/// On the in-memory bus the handler runs in the publisher's scope (synchronous dispatch), so any
/// exception surfaces to that request; the work is kept small for that reason.
/// </summary>
public sealed class UserNotificationRequestedIntegrationEventHandler(
    NotificationsDbContext db,
    IHubContext<AppHub> hub,
    ITenantAccessor tenantAccessor,
    ILogger<UserNotificationRequestedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<UserNotificationRequestedIntegrationEvent>
{
    public async Task HandleAsync(UserNotificationRequestedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Fail loud on a tenant mismatch rather than write to the wrong tenant: the DbContext captures its
        // tenant at construction, so a future publisher omitting the context would leak cross-tenant silently.
        var ambientTenantId = tenantAccessor.TenantInfo?.Id;
        if (!string.Equals(ambientTenantId, @event.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Tenant context mismatch handling {nameof(UserNotificationRequestedIntegrationEvent)}: ambient " +
                $"'{ambientTenantId ?? "(none)"}' != event '{@event.TenantId ?? "(none)"}'. The publisher must " +
                "establish the tenant's Finbuckle context before publishing (see eventing rules).");
        }

        var notification = Notification.Create(
            userId: @event.UserId,
            type: @event.Type,
            title: @event.Title,
            body: @event.Body,
            link: @event.Link,
            source: @event.Source,
            metadata: @event.Metadata);

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await hub.Clients.Group($"user:{@event.UserId}")
            .SendAsync("NotificationCreated", new
            {
                id = notification.Id,
                type = notification.Type,
                title = notification.Title,
                body = notification.Body,
                link = notification.Link,
                source = notification.Source,
                createdAtUtc = notification.CreatedAtUtc,
            }, ct)
            .ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Notification {NotificationId} of type {Type} for user {UserId} from {Source}",
                notification.Id, notification.Type, @event.UserId, @event.Source);
        }
    }
}
