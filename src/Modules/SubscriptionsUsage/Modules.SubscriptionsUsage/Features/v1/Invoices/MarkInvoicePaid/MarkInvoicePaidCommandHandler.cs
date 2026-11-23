using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;
using Trislab.Modules.SubscriptionsUsage.Services;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.MarkInvoicePaid;

public sealed class MarkInvoicePaidCommandHandler(IBillingService billing)
    : ICommandHandler<MarkInvoicePaidCommand, Guid>
{
    public async ValueTask<Guid> Handle(MarkInvoicePaidCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await billing.MarkInvoicePaidAsync(command.InvoiceId, cancellationToken).ConfigureAwait(false);
        return command.InvoiceId;
    }
}
