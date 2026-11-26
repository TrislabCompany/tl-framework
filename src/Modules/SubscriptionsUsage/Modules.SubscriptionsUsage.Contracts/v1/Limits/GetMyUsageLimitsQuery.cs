using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;

/// <summary>The current tenant's usage of every meter against its plan.</summary>
public sealed record GetMyUsageLimitsQuery : IQuery<IReadOnlyList<UsageLimitStatusDto>>;
