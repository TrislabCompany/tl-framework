using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;
using Trislab.Modules.SubscriptionsUsage.Services;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.VoidInvoice;

public sealed class VoidInvoiceCommandHandler(IBillingService billing)
    : ICommandHandler<VoidInvoiceCommand, Guid>
{
    public async ValueTask<Guid> Handle(VoidInvoiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await billing.VoidInvoiceAsync(command.InvoiceId, command.Reason, cancellationToken).ConfigureAwait(false);
        return command.InvoiceId;
    }
}
