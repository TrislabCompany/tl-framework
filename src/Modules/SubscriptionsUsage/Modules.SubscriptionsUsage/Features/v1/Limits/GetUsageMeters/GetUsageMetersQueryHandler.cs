using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.Limits;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.GetUsageMeters;

public sealed class GetUsageMetersQueryHandler(IEnumerable<IUsageMeter> meters)
    : IQueryHandler<GetUsageMetersQuery, IReadOnlyList<UsageMeterDto>>
{
    public ValueTask<IReadOnlyList<UsageMeterDto>> Handle(GetUsageMetersQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<UsageMeterDto> result = meters
            .OrderBy(m => m.Key, StringComparer.Ordinal)
            .Select(m => new UsageMeterDto(m.Key, m.Name, m.Mode, m.DefaultLimit, m is IUsageGauge))
            .ToList();
        return ValueTask.FromResult(result);
    }
}
