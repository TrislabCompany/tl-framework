using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<UpdateCustomerCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.CustomerNotFound(command.CustomerId);

        // Documents already issued keep the copy of the details they were issued with.
        customer.Update(
            command.Name, command.Email, command.AddressLine, command.PostalCode, command.City,
            command.CountryCode, command.TaxNumber, command.Note);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return customer.Id;
    }
}
