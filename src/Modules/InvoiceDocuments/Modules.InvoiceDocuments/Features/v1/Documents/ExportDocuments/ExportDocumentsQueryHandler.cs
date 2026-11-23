using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ExportDocuments;

/// <summary>The accountant's export: every document issued from <c>From</c> to <c>To</c>, as a CSV and one PDF each.</summary>
public sealed class ExportDocumentsQueryHandler(InvoiceDocumentsDbContext dbContext, IDocumentPdfRenderer renderer)
    : IQueryHandler<ExportDocumentsQuery, DocumentFileDto>
{
    public async ValueTask<DocumentFileDto> Handle(ExportDocumentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var documents = await dbContext.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Draft && d.IssueDate >= query.From && d.IssueDate <= query.To)
            .OrderBy(d => d.IssueDate).ThenBy(d => d.Number)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new DocumentFileDto(
            DocumentExportBuilder.Build(documents, renderer),
            $"documents-{query.From:yyyy-MM-dd}-{query.To:yyyy-MM-dd}.zip",
            "application/zip");
    }
}
