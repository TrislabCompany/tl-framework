using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler(
    BillingDbContext dbContext,
    ITenantAccessor tenantAccessor)
    : IQueryHandler<GetInvoiceByIdQuery, InvoiceDto>
{
    public async ValueTask<InvoiceDto> Handle(GetInvoiceByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext isn't tenant-filtered (raw DbContext for cross-tenant admin visibility): root
        // reads any invoice by id; a tenant caller is pinned to its own so it can't read another's. Mirrors GetSubscriptionQueryHandler.
        var callerTenantId = tenantAccessor.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;

        var invoice = await dbContext.Invoices.AsNoTracking()
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(
                i => i.Id == query.InvoiceId && (isRoot || i.TenantId == callerTenantId),
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Invoice {query.InvoiceId} not found.")
                .Localized<BillingResources>("Billing.InvoiceNotFound", query.InvoiceId);

        return invoice.ToDto();
    }
}
