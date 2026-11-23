using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.DeleteItem;

public sealed class DeleteItemCommandValidator : AbstractValidator<DeleteItemCommand>
{
    public DeleteItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
    }
}
