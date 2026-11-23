using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.SendDocument;

public sealed class SendDocumentCommandValidator : AbstractValidator<SendDocumentCommand>
{
    public SendDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.ToEmail).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.ToEmail));
        RuleFor(x => x.Message).MaximumLength(2000);
    }
}
