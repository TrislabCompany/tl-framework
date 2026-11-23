using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;

public sealed record IssueInvoiceCommand(Guid InvoiceId, DateTime? DueAtUtc = null) : ICommand<Guid>;
