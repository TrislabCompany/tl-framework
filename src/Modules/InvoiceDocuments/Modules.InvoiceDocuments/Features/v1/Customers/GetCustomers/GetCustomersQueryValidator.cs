using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.GetCustomers;

public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public GetCustomersQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
