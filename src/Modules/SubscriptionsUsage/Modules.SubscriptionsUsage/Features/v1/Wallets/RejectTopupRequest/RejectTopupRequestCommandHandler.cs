using System.Net;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.SubscriptionsUsage.Contracts;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Wallets;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.RejectTopupRequest;

public sealed class RejectTopupRequestCommandHandler(
    BillingDbContext db,
    ITenantAccessor tenantAccessor)
    : ICommandHandler<RejectTopupRequestCommand, Guid>
{
    public async ValueTask<Guid> Handle(RejectTopupRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var callerTenantId = tenantAccessor.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;

        var request = await db.TopupRequests
            .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Top-up request {command.Id} not found.")
                .Localized<BillingResources>("Billing.TopupRequestNotFound", command.Id);

        if (!isRoot && request.TenantId != callerTenantId)
        {
            throw new UnauthorizedException("You can only reject top-up requests for your own tenant.")
                .Localized<BillingResources>("Billing.CannotRejectTopupForOtherTenant");
        }

        if (request.Status != TopupRequestStatus.Pending)
        {
            throw new CustomException(
                $"Top-up request {command.Id} cannot be rejected because it is {request.Status} (only Pending requests can be rejected).",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict)
                .Localized<BillingResources>("Billing.TopupRequestCannotBeRejected", command.Id, request.Status);
        }

        request.Reject(command.Reason);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Id;
    }
}
