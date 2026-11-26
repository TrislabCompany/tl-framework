using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.Limits;

/// <summary>
/// Checks and counts the current tenant's usage against its plan's limits. Every call works on the tenant of
/// the current scope (a request, or a job's <c>ITenantAccessor.Use</c>). The root tenant is never limited.
/// </summary>
public interface IUsageLimits
{
    /// <summary>Whether <paramref name="amount"/> more units of the meter fit the limit. Changes nothing.</summary>
    ValueTask<UsageLimitStatusDto> CheckAsync(string meterKey, long amount = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws a 409 with a clear, localized message when <paramref name="amount"/> more units of a hard-limited meter
    /// don't fit. Call it before the action, for a gauge (adding a member) or a counter. Changes nothing.
    /// </summary>
    ValueTask EnsureWithinLimitAsync(string meterKey, long amount = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="amount"/> units to a counter for the current period, atomically: a hard limit is never
    /// passed, even by concurrent calls (a 409 instead, and nothing counted); extra usage is always counted.
    /// Not for gauges.
    /// </summary>
    ValueTask<UsageLimitStatusDto> ConsumeAsync(string meterKey, long amount = 1, CancellationToken cancellationToken = default);

    /// <summary>Every declared meter with the tenant's usage and limit, for the tenant's usage page.</summary>
    ValueTask<IReadOnlyList<UsageLimitStatusDto>> GetUsageAsync(CancellationToken cancellationToken = default);
}
