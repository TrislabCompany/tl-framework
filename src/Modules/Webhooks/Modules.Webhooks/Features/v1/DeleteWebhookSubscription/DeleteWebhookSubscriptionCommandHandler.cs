using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Webhooks.Contracts.v1.DeleteWebhookSubscription;
using Trislab.Modules.Webhooks.Data;
using Trislab.Modules.Webhooks.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Webhooks.Features.v1.DeleteWebhookSubscription;

public sealed class DeleteWebhookSubscriptionCommandHandler(
    WebhookDbContext dbContext) : ICommandHandler<DeleteWebhookSubscriptionCommand>
{
    public async ValueTask<Unit> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subscription = await dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Webhook subscription {command.Id} not found.")
                .Localized<WebhooksResources>("Webhooks.SubscriptionNotFound", command.Id);

        dbContext.Subscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
