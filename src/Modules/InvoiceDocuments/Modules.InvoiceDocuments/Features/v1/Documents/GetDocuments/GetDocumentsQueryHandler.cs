using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocuments;

public sealed class GetDocumentsQueryHandler(InvoiceDocumentsDbContext dbContext)
    : IQueryHandler<GetDocumentsQuery, PagedResponse<DocumentSummaryDto>>
{
    public async ValueTask<PagedResponse<DocumentSummaryDto>> Handle(GetDocumentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = DocumentMappings.Today();
        var q = dbContext.Documents.AsNoTracking().AsQueryable();
        if (query.Type is { } type)
        {
            q = q.Where(d => d.Type == type);
        }
        if (query.Status is { } status)
        {
            q = q.Where(d => d.Status == status);
        }
        if (query.CustomerId is { } customerId)
        {
            q = q.Where(d => d.CustomerId == customerId);
        }
        if (query.From is { } from)
        {
            q = q.Where(d => d.IssueDate >= from);
        }
        if (query.To is { } to)
        {
            q = q.Where(d => d.IssueDate <= to);
        }
        if (query.OpenOnly || query.OverdueOnly)
        {
            // Open: an issued invoice or advance invoice that is not paid yet. Overdue: open and past its due date.
            q = q.Where(d => d.Status == DocumentStatus.Issued
                && (d.Type == DocumentType.Invoice || d.Type == DocumentType.AdvanceInvoice));
        }
        if (query.OverdueOnly)
        {
            q = q.Where(d => d.DueDate != null && d.DueDate < today);
        }

        var total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await q
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip(PaginationExtensions.GetOffset(query.PageNumber, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<DocumentSummaryDto>
        {
            Items = rows.Select(d => d.ToSummary(today)).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize),
        };
    }
}
