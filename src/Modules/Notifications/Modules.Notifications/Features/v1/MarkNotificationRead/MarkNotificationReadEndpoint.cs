using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Notifications.Contracts.Authorization;
using Trislab.Modules.Notifications.Contracts.v1.Commands;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Notifications.Features.v1.MarkNotificationRead;

public static class MarkNotificationReadEndpoint
{
    internal static RouteHandlerBuilder MapMarkNotificationReadEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/{id:guid}/read",
                async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    await mediator.Send(new MarkNotificationReadCommand(id), cancellationToken);
                    return Results.NoContent();
                })
            .WithName("MarkNotificationRead")
            .WithSummary("Mark a single notification as read")
            .RequirePermission(NotificationPermissions.Inbox.MarkRead);
}
