using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Webhooks.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Webhooks.Contracts.v1.GetWebhookDeliveries;

public sealed record GetWebhookDeliveriesQuery(Guid SubscriptionId, int PageNumber = 1, int PageSize = 10)
    : IQuery<PagedResponse<WebhookDeliveryDto>>;
