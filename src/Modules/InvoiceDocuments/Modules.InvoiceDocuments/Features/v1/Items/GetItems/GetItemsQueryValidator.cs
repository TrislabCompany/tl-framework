using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.GetItems;

public sealed class GetItemsQueryValidator : AbstractValidator<GetItemsQuery>
{
    public GetItemsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
