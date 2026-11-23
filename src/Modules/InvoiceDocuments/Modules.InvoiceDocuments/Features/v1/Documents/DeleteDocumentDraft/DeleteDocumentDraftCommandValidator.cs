using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.DeleteDocumentDraft;

public sealed class DeleteDocumentDraftCommandValidator : AbstractValidator<DeleteDocumentDraftCommand>
{
    public DeleteDocumentDraftCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}
