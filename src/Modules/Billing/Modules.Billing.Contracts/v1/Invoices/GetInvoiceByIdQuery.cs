using Trislab.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Billing.Contracts.v1.Invoices;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IQuery<InvoiceDto>;
