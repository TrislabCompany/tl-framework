using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Mappings;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.GetTopupRequests;

public sealed class GetTopupRequestsQueryHandler(
    BillingDbContext dbContext,
    ITenantAccessor tenantAccessor)
    : IQueryHandler<GetTopupRequestsQuery, PagedResponse<TopupRequestDto>>
{
    public async ValueTask<PagedResponse<TopupRequestDto>> Handle(GetTopupRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext is not tenant-filtered: only root gets the cross-tenant view (optionally
        // narrowed via query.TenantId); every other caller is forced to its own tenant.
        var callerTenantId = tenantAccessor.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;
        var tenantFilter = isRoot ? query.TenantId : callerTenantId;

        var q = dbContext.TopupRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantFilter))
        {
            q = q.Where(r => r.TenantId == tenantFilter);
        }
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
