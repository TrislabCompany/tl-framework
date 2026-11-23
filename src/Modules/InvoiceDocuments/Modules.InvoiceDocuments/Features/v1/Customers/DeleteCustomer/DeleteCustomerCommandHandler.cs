using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.DeleteCustomer;

public sealed class DeleteCustomerCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<DeleteCustomerCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteCustomerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.CustomerNotFound(command.CustomerId);

        // A soft delete: issued documents keep their own copy of the details, and drafts keep the link.
        customer.Delete(deletedBy: null);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return customer.Id;
    }
}
