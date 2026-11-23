using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocumentPdf;

public sealed class GetDocumentPdfQueryHandler(InvoiceDocumentsDbContext dbContext, IDocumentPdfRenderer renderer)
    : IQueryHandler<GetDocumentPdfQuery, DocumentFileDto>
{
    public async ValueTask<DocumentFileDto> Handle(GetDocumentPdfQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var document = await dbContext.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == query.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(query.DocumentId);
        if (document.Status == DocumentStatus.Draft)
        {
            throw DocumentErrors.NotIssued();
        }

        return new DocumentFileDto(
            renderer.Render(document.ToDto()),
            $"{DocumentExportBuilder.FileSafe(document.Number!)}.pdf",
            "application/pdf");
    }
}
