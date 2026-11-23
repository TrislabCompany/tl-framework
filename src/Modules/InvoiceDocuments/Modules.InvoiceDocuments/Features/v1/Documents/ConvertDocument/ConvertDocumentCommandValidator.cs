using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ConvertDocument;

public sealed class ConvertDocumentCommandValidator : AbstractValidator<ConvertDocumentCommand>
{
    public ConvertDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.TargetType).IsInEnum();
    }
}
