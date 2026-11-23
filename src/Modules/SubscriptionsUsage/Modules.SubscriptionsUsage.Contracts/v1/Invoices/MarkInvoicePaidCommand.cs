using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;

public sealed record MarkInvoicePaidCommand(Guid InvoiceId) : ICommand<Guid>;
