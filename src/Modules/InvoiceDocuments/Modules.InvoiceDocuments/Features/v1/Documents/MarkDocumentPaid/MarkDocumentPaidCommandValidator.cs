using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.MarkDocumentPaid;

public sealed class MarkDocumentPaidCommandValidator : AbstractValidator<MarkDocumentPaidCommand>
{
    public MarkDocumentPaidCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.PaidOnUtc).LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5)).When(x => x.PaidOnUtc.HasValue);
    }
}
