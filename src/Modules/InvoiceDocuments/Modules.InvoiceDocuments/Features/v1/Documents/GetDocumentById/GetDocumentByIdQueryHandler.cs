using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocumentById;

public sealed class GetDocumentByIdQueryHandler(InvoiceDocumentsDbContext dbContext)
    : IQueryHandler<GetDocumentByIdQuery, DocumentDto>
{
    public async ValueTask<DocumentDto> Handle(GetDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var document = await dbContext.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == query.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(query.DocumentId);
        return document.ToDto();
    }
}
