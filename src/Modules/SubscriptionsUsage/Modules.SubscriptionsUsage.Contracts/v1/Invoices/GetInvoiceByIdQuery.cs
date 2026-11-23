using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IQuery<InvoiceDto>;
