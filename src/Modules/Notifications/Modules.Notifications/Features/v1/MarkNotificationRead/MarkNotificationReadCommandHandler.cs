using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Notifications.Contracts.v1.Commands;
using Trislab.Modules.Notifications.Data;
using Trislab.Modules.Notifications.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Notifications.Features.v1.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    NotificationsDbContext db,
    ICurrentUser currentUser)
    : ICommandHandler<MarkNotificationReadCommand, Unit>
{
    public async ValueTask<Unit> Handle(MarkNotificationReadCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedException("no current user").Localized("Error.NoCurrentUser");
        }
        var currentUserId = userId.ToString();

        // Caller-scoped: filter by (Id, UserId) so users can only mutate their own rows. Returns
        // 404 if the row exists but belongs to someone else — we don't leak existence.
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == cmd.NotificationId && n.UserId == currentUserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Notification not found.")
                .Localized<NotificationsResources>("Notifications.NotificationNotFound");

        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
