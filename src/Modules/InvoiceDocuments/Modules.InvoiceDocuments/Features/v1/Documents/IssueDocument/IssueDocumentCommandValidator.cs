using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.IssueDocument;

public sealed class IssueDocumentCommandValidator : AbstractValidator<IssueDocumentCommand>
{
    public IssueDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}
