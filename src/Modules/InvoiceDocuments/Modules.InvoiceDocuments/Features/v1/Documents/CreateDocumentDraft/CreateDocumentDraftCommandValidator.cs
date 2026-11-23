using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.CreateDocumentDraft;

public sealed class CreateDocumentDraftCommandValidator : AbstractValidator<CreateDocumentDraftCommand>
{
    public CreateDocumentDraftCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Currency).Length(3).When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ReferenceDocumentId).NotEmpty().When(x => x.Type == DocumentType.CreditNote);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 200);
        RuleForEach(x => x.Lines).SetValidator(new DocumentLineInputValidator());
    }
}

/// <summary>The rules of one line, shared by creating and updating a draft.</summary>
public sealed class DocumentLineInputValidator : AbstractValidator<DocumentLineInput>
{
    public DocumentLineInputValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).When(x => x.UnitPrice.HasValue);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.TaxCode).MaximumLength(32);
        RuleFor(x => x).Must(x => x.ItemId.HasValue
                || (!string.IsNullOrWhiteSpace(x.Description) && x.UnitPrice.HasValue && !string.IsNullOrWhiteSpace(x.TaxCode)))
            .WithMessage("A line needs an item, or a description, a unit price and a tax code.");
    }
}
