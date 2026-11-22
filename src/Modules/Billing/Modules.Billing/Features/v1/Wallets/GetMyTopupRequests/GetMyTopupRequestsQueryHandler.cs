using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Billing.Contracts.Dtos;
using Trislab.Modules.Billing.Contracts.v1.Wallets;
using Trislab.Modules.Billing.Data;
using Trislab.Modules.Billing.Mappings;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Billing.Features.v1.Wallets.GetMyTopupRequests;

public sealed class GetMyTopupRequestsQueryHandler(
    BillingDbContext dbContext,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetMyTopupRequestsQuery, PagedResponse<TopupRequestDto>>
{
    public async ValueTask<PagedResponse<TopupRequestDto>> Handle(GetMyTopupRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext is not tenant-filtered; resolve caller's own tenant and scope strictly to it.
        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");

        var q = dbContext.TopupRequests.AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (query.Status is not null)
        {
            q = q.Where(r => r.Status == query.Status);
        }

        var total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(PaginationExtensions.GetOffset(query.PageNumber, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<TopupRequestDto>
        {
            Items = items.Select(r => r.ToDto()).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize)
        };
    }
}
