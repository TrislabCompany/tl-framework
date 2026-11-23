using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;

public sealed record VoidInvoiceCommand(Guid InvoiceId, string? Reason = null) : ICommand<Guid>;
