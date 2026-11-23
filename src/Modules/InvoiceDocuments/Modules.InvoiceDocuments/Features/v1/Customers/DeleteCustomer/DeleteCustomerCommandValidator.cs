using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.DeleteCustomer;

public sealed class DeleteCustomerCommandValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
