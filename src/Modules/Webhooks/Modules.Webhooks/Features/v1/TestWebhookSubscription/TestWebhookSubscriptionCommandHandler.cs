using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Webhooks.Contracts.v1.TestWebhookSubscription;
using Trislab.Modules.Webhooks.Data;
using Trislab.Modules.Webhooks.Localization;
using Trislab.Modules.Webhooks.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Trislab.Modules.Webhooks.Features.v1.TestWebhookSubscription;

public sealed class TestWebhookSubscriptionCommandHandler(
    WebhookDbContext dbContext,
    IWebhookDeliveryService deliveryService,
    IWebhookSecretProtector secretProtector) : ICommandHandler<TestWebhookSubscriptionCommand, bool>
{
    public async ValueTask<bool> Handle(TestWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Webhook subscription {command.Id} not found.")
                .Localized<WebhooksResources>("Webhooks.SubscriptionNotFound", command.Id);

        var testPayload = JsonSerializer.Serialize(new
        {
            eventType = "webhook.test",
            timestamp = TimeProvider.System.GetUtcNow().UtcDateTime,
            message = "This is a test webhook delivery."
        });

        await deliveryService.DeliverAsync(
            subscription.Id,
            subscription.Url,
            secretProtector.Unprotect(subscription.ProtectedSecret),
            "webhook.test",
            testPayload,
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
