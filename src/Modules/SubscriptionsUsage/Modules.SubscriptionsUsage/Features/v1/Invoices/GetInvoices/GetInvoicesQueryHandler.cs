using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;
using Trislab.Modules.SubscriptionsUsage.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GetInvoices;

public sealed class GetInvoicesQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetInvoicesQuery, PagedResponse<InvoiceDto>>
{
    public async ValueTask<PagedResponse<InvoiceDto>> Handle(GetInvoicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext is not tenant-filtered: only root gets the cross-tenant view (optionally
        // narrowed via query.TenantId); every other caller is forced to its own tenant.
        var callerTenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;
        var tenantFilter = isRoot ? query.TenantId : callerTenantId;

        var q = dbContext.Invoices.AsNoTracking().Include(i => i.LineItems).AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantFilter))
        {
            q = q.Where(i => i.TenantId == tenantFilter);
        }
        if (query.Status is not null)
        {
            q = q.Where(i => i.Status == query.Status);
        }
        if (query.PeriodYear is not null)
        {
            q = q.Where(i => i.PeriodYear == query.PeriodYear);
        }
        if (query.PeriodMonth is not null)
        {
            q = q.Where(i => i.PeriodMonth == query.PeriodMonth);
        }

        var total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var invoices = await q
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip(PaginationExtensions.GetOffset(query.PageNumber, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<InvoiceDto>
        {
            Items = invoices.Select(i => i.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize)
        };
    }
}
