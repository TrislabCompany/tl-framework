using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.Limits;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.GetMyUsageLimits;

public sealed class GetMyUsageLimitsQueryHandler(IUsageLimits limits)
    : IQueryHandler<GetMyUsageLimitsQuery, IReadOnlyList<UsageLimitStatusDto>>
{
    public ValueTask<IReadOnlyList<UsageLimitStatusDto>> Handle(GetMyUsageLimitsQuery query, CancellationToken cancellationToken) =>
        limits.GetUsageAsync(cancellationToken);
}
