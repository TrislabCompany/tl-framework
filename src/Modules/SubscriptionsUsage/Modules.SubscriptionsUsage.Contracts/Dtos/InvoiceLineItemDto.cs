using Trislab.Framework.Shared.Quota;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;

public sealed record InvoiceLineItemDto(
    Guid Id,
    InvoiceLineItemKind Kind,
    QuotaResource? Resource,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);
