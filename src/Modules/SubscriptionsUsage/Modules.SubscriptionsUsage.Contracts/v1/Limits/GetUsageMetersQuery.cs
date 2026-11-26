using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;

/// <summary>The meters the product declared (root, for the plan editor).</summary>
public sealed record GetUsageMetersQuery : IQuery<IReadOnlyList<UsageMeterDto>>;
