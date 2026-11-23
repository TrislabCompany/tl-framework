using Trislab.Framework.Shared.Quota;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Plans;

public sealed record UpdatePlanCommand(
    Guid PlanId,
    string Name,
    decimal MonthlyBasePrice,
    IReadOnlyDictionary<QuotaResource, decimal>? OverageRates = null,
    PlanInterval Interval = PlanInterval.Monthly,
    decimal? AnnualPrice = null) : ICommand<Guid>;
