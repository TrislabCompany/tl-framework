using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Usage;
using Trislab.Modules.SubscriptionsUsage.Services;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Usage.CaptureUsageSnapshots;

public sealed class CaptureUsageSnapshotsCommandHandler(
    IUsageReporter reporter,
    ITenantAccessor tenantAccessor)
    : ICommandHandler<CaptureUsageSnapshotsCommand, IReadOnlyList<UsageSnapshotDto>>
{
    public async ValueTask<IReadOnlyList<UsageSnapshotDto>> Handle(
        CaptureUsageSnapshotsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Only the root operator may capture usage for an arbitrary tenant; a tenant caller is pinned
        // to its own tenant so it can't fabricate another tenant's usage/overage snapshots.
        var callerTenantId = tenantAccessor.TenantInfo?.Id
            ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");
        var isRoot = callerTenantId == MultitenancyConstants.Root.Id;
        var targetTenantId = isRoot ? command.TenantId : callerTenantId;

        var snapshots = await reporter
            .CaptureForPeriodAsync(targetTenantId, command.PeriodYear, command.PeriodMonth, cancellationToken)
            .ConfigureAwait(false);

        return snapshots
            .Select(s => new UsageSnapshotDto(
                s.Id,
                s.TenantId,
                s.PeriodYear,
                s.PeriodMonth,
                s.Resource,
                s.UsedUnits,
                s.LimitUnits,
                s.Overage,
                s.CapturedAtUtc))
            .ToList();
    }
}
