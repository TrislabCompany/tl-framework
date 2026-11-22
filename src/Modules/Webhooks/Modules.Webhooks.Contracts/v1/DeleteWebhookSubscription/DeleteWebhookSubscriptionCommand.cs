using Mediator;

namespace Trislab.Modules.Webhooks.Contracts.v1.DeleteWebhookSubscription;

public sealed record DeleteWebhookSubscriptionCommand(Guid Id) : ICommand;
