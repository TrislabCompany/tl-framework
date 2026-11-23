using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Usage;

public sealed record GetUsageSnapshotsQuery(
    string? TenantId = null,
    int? PeriodYear = null,
    int? PeriodMonth = null) : IQuery<IReadOnlyList<UsageSnapshotDto>>;
