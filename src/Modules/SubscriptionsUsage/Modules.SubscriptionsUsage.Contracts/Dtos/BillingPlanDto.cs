using Trislab.Framework.Shared.Quota;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;

public sealed record BillingPlanDto(
    Guid Id,
    string Key,
    string Name,
    string Currency,
    decimal MonthlyBasePrice,
    IReadOnlyDictionary<QuotaResource, decimal> OverageRates,
    bool IsActive,
    PlanInterval Interval,
    decimal? AnnualPrice,
    IReadOnlyList<PlanLimitDto> Limits);
