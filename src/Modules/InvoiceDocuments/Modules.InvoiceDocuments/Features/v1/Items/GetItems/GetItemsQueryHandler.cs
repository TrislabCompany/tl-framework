using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.GetItems;

public sealed class GetItemsQueryHandler(InvoiceDocumentsDbContext dbContext)
    : IQueryHandler<GetItemsQuery, PagedResponse<ItemDto>>
{
    public async ValueTask<PagedResponse<ItemDto>> Handle(GetItemsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Items.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(i => EF.Functions.ILike(i.Name, pattern));
        }

        var total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await q
            .OrderBy(i => i.Name)
            .Skip(PaginationExtensions.GetOffset(query.PageNumber, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<ItemDto>
        {
            Items = rows.Select(i => i.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize),
        };
    }
}
