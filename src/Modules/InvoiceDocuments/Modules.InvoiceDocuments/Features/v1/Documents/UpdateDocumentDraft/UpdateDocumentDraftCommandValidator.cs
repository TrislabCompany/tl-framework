using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.CreateDocumentDraft;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.UpdateDocumentDraft;

public sealed class UpdateDocumentDraftCommandValidator : AbstractValidator<UpdateDocumentDraftCommand>
{
    public UpdateDocumentDraftCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 200);
        RuleForEach(x => x.Lines).SetValidator(new DocumentLineInputValidator());
    }
}
