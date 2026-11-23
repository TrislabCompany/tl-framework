using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.GetCustomers;

public sealed class GetCustomersQueryHandler(InvoiceDocumentsDbContext dbContext)
    : IQueryHandler<GetCustomersQuery, PagedResponse<CustomerDto>>
{
    public async ValueTask<PagedResponse<CustomerDto>> Handle(GetCustomersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.Name, pattern) || (c.TaxNumber != null && EF.Functions.ILike(c.TaxNumber, pattern)));
        }

        var total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await q
            .OrderBy(c => c.Name)
            .Skip(PaginationExtensions.GetOffset(query.PageNumber, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<CustomerDto>
        {
            Items = rows.Select(c => c.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize),
        };
    }
}
