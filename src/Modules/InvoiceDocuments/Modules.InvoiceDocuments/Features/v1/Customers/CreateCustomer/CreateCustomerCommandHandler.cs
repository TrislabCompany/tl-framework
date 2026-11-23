using Mediator;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.CreateCustomer;

public sealed class CreateCustomerCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customer = Customer.Create(
            command.Name, command.Email, command.AddressLine, command.PostalCode, command.City,
            command.CountryCode, command.TaxNumber, command.Note);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return customer.Id;
    }
}
