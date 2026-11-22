using Trislab.Framework.Eventing.Abstractions;

namespace Trislab.Modules.Notifications.Contracts.Events;

/// <summary>
/// Asks the Notifications module to put one row in a user's inbox (the bell) and push it to the
/// user's open connections. Any module may publish it; the publisher owns the wording, the link and
/// the metadata, so Notifications never depends on the module that raised it.
/// <c>Type</c> is a logical type such as <c>chat.mention</c> (the UI picks the icon from it); <c>Body</c>
/// is already shortened by the publisher; <c>Link</c> is the app route the notification opens; and
/// <c>Metadata</c> is kept with the row as JSON.
/// </summary>
public sealed record UserNotificationRequestedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    string UserId,
    string Type,
    string Title,
    string? Body,
    string? Link,
    IReadOnlyDictionary<string, string?>? Metadata) : IIntegrationEvent;
