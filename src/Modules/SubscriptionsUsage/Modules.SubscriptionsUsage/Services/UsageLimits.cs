using System.Net;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.SubscriptionsUsage.Contracts;
using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.Limits;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Domain;
using Trislab.Modules.SubscriptionsUsage.Localization;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Services;

/// <summary>
/// The limits of the product-declared meters (<see cref="IUsageMeter"/>) for the current tenant. The tenant's plan is
/// its active subscription's plan, else the plan named on the tenant; a meter the plan sets no limit for uses its
/// default. Counters live in <see cref="BillingDbContext.UsageCounters"/> and change only through one atomic upsert.
/// </summary>
internal sealed class UsageLimits(
    BillingDbContext dbContext,
    ITenantAccessor tenantAccessor,
    IEnumerable<IUsageMeter> meters,
    TimeProvider timeProvider) : IUsageLimits
{
    private readonly Dictionary<string, IUsageMeter> _meters = BuildCatalog(meters);

    public async ValueTask<UsageLimitStatusDto> CheckAsync(string meterKey, long amount = 1, CancellationToken cancellationToken = default)
    {
        var meter = Find(meterKey);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var tenantId = CurrentTenantId();
        var plan = await FindPlanAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var used = await GetUsedAsync(meter, tenantId, cancellationToken).ConfigureAwait(false);
        return Status(meter, plan, tenantId, used, amount);
    }

    public async ValueTask EnsureWithinLimitAsync(string meterKey, long amount = 1, CancellationToken cancellationToken = default)
    {
        var status = await CheckAsync(meterKey, amount, cancellationToken).ConfigureAwait(false);
        if (!status.Allowed)
        {
            throw LimitReached(status);
        }
    }

    public async ValueTask<UsageLimitStatusDto> ConsumeAsync(string meterKey, long amount = 1, CancellationToken cancellationToken = default)
    {
        var meter = Find(meterKey);
        if (meter is IUsageGauge)
        {
            throw new InvalidOperationException($"Meter '{meterKey}' is a gauge: check it with EnsureWithinLimitAsync, never consume it.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var tenantId = CurrentTenantId();
        var plan = await FindPlanAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var (limit, _) = Resolve(meter, plan, tenantId);
        var hardLimit = meter.Mode == UsageLimitMode.HardLimit ? limit : null;

        if (hardLimit is { } max && amount > max)
        {
            var used = await GetUsedAsync(meter, tenantId, cancellationToken).ConfigureAwait(false);
            throw LimitReached(Status(meter, plan, tenantId, used, amount));
        }

        // One statement: insert the period's row, or add to it only while the hard limit holds. No row back means
        // the limit would be passed, and nothing was counted. The limit is a parameter, so a null means "no check".
        var periodStart = CurrentPeriodStart();
        var key = meter.Key;
        // The schema and table are written out: an interpolated value would become a parameter.
        var rows = await dbContext.Database.SqlQuery<long>(
            $"""
            INSERT INTO billing."UsageCounters" ("TenantId", "MeterKey", "PeriodStart", "Used")
            VALUES ({tenantId}, {key}, {periodStart}, {amount})
            ON CONFLICT ("TenantId", "MeterKey", "PeriodStart")
            DO UPDATE SET "Used" = "UsageCounters"."Used" + EXCLUDED."Used"
            WHERE {hardLimit}::bigint IS NULL OR "UsageCounters"."Used" + EXCLUDED."Used" <= {hardLimit}::bigint
            RETURNING "Used" AS "Value"
            """)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            var used = await GetUsedAsync(meter, tenantId, cancellationToken).ConfigureAwait(false);
            throw LimitReached(Status(meter, plan, tenantId, used, amount));
        }

        return Status(meter, plan, tenantId, rows[0], 0);
    }

    public async ValueTask<IReadOnlyList<UsageLimitStatusDto>> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        var plan = await FindPlanAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var result = new List<UsageLimitStatusDto>(_meters.Count);
        foreach (var meter in _meters.Values.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            var used = await GetUsedAsync(meter, tenantId, cancellationToken).ConfigureAwait(false);
            result.Add(Status(meter, plan, tenantId, used, 0));
        }
        return result;
    }

    private static Dictionary<string, IUsageMeter> BuildCatalog(IEnumerable<IUsageMeter> meters)
    {
        var catalog = new Dictionary<string, IUsageMeter>(StringComparer.Ordinal);
        foreach (var meter in meters)
        {
            if (!catalog.TryAdd(meter.Key, meter))
            {
                throw new InvalidOperationException($"Two usage meters are declared with the key '{meter.Key}'.");
            }
        }
        return catalog;
    }

    private IUsageMeter Find(string meterKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meterKey);
        return _meters.TryGetValue(meterKey, out var meter)
            ? meter
            : throw new InvalidOperationException($"No usage meter is declared with the key '{meterKey}'.");
    }

    private string CurrentTenantId() =>
        tenantAccessor.TenantInfo?.Id
        ?? throw new UnauthorizedException("Tenant context is required.").Localized("Error.TenantContextRequired");

    private static bool IsRoot(string tenantId) =>
        string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal);

    private async ValueTask<BillingPlan?> FindPlanAsync(string tenantId, CancellationToken cancellationToken)
    {
        var planId = await dbContext.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartUtc)
            .Select(s => (Guid?)s.PlanId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (planId is { } id)
        {
            return await dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
        }

        var planKey = tenantAccessor.TenantInfo?.Plan;
        if (string.IsNullOrWhiteSpace(planKey))
        {
            return null;
        }
#pragma warning disable CA1308 // Plan keys are canonical lowercase slugs
        var key = planKey.ToLowerInvariant();
#pragma warning restore CA1308
        return await dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Key == key, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<long> GetUsedAsync(IUsageMeter meter, string tenantId, CancellationToken cancellationToken)
    {
        if (meter is IUsageGauge gauge)
        {
            return await gauge.GetCurrentAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        var periodStart = CurrentPeriodStart();
        return await dbContext.UsageCounters.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.MeterKey == meter.Key && c.PeriodStart == periodStart)
            .Select(c => c.Used)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The limit and extra unit price for the tenant: none for root, else the plan's, else the meter's default.</summary>
    private static (long? Limit, decimal? ExtraUnitPrice) Resolve(IUsageMeter meter, BillingPlan? plan, string tenantId)
    {
        if (IsRoot(tenantId))
        {
            return (null, null);
        }
        var planLimit = plan?.FindLimit(meter.Key);
        return planLimit is null ? (meter.DefaultLimit, null) : (planLimit.Limit, planLimit.ExtraUnitPrice);
    }

    private static UsageLimitStatusDto Status(IUsageMeter meter, BillingPlan? plan, string tenantId, long used, long amount)
    {
        var (limit, extraUnitPrice) = Resolve(meter, plan, tenantId);
        var allowed = meter.Mode == UsageLimitMode.ExtraUsage || limit is null || used + amount <= limit;
        return new UsageLimitStatusDto(
            meter.Key,
            meter.Name,
            meter.Mode,
            used,
            limit,
            allowed,
            meter.Mode == UsageLimitMode.ExtraUsage ? extraUnitPrice : null,
            meter.Mode == UsageLimitMode.ExtraUsage && extraUnitPrice is not null ? plan?.Currency : null);
    }

    private static CustomException LimitReached(UsageLimitStatusDto status) =>
        new CustomException(
                $"The plan allows {status.Limit} of '{status.MeterKey}'; {status.Used} are used.",
                Array.Empty<string>(),
                HttpStatusCode.Conflict)
            .Localized<BillingResources>("Billing.LimitReached", status.Name, status.Limit ?? 0, status.Used);

    /// <summary>The first day of the current calendar month (UTC): the counters' period until tenants get their own.</summary>
    private DateOnly CurrentPeriodStart()
    {
        var now = timeProvider.GetUtcNow();
        return new DateOnly(now.Year, now.Month, 1);
    }
}
