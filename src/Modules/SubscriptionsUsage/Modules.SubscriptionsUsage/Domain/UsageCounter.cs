namespace Trislab.Modules.SubscriptionsUsage.Domain;

/// <summary>
/// What a tenant consumed of one counter meter in one period (AI pages this month). One row per
/// (tenant, meter, period start), changed only by one atomic SQL statement in <c>UsageLimits</c>, so concurrent
/// consumers never pass a hard limit. The period is the calendar month (UTC) until each tenant gets its own
/// billing period.
/// </summary>
public sealed class UsageCounter
{
    public string TenantId { get; init; } = default!;
    public string MeterKey { get; init; } = default!;
    public DateOnly PeriodStart { get; init; }
    public long Used { get; init; }
}
