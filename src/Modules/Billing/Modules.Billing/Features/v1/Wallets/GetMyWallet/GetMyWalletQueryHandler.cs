using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Billing.Contracts.Dtos;
using Trislab.Modules.Billing.Contracts.v1.Wallets;
using Trislab.Modules.Billing.Mappings;
using Trislab.Modules.Billing.Services;
using Mediator;

namespace Trislab.Modules.Billing.Features.v1.Wallets.GetMyWallet;

public sealed class GetMyWalletQueryHandler(
    IBillingService billingService,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : IQueryHandler<GetMyWalletQuery, WalletDto>
{
    public async ValueTask<WalletDto> Handle(GetMyWalletQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // BillingDbContext is not tenant-filtered; resolve caller's own tenant and scope strictly to it.
        var tenantId = tenantAccessor.MultiTenantContext?.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");

        var wallet = await billingService.GetOrCreateWalletAsync(tenantId, "USD", cancellationToken).ConfigureAwait(false);
        return wallet.ToDto();
    }
}
