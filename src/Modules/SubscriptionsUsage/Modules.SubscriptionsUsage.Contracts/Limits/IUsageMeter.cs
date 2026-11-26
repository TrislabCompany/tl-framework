namespace Trislab.Modules.SubscriptionsUsage.Contracts.Limits;

/// <summary>
/// A thing a product counts per tenant and limits by plan, declared by the product (or a module) in code:
/// <c>services.AddSingleton&lt;IUsageMeter, AiPagesMeter&gt;()</c>. A plain meter is a <b>counter</b> for the
/// period (AI pages read this month): the product adds to it with <see cref="IUsageLimits.ConsumeAsync"/>.
/// A meter that counts what exists right now (members, items) implements <see cref="IUsageGauge"/> instead.
/// Root sets each plan's limit per meter; <see cref="DefaultLimit"/> applies where a plan sets none.
/// </summary>
public interface IUsageMeter
{
    /// <summary>A stable kebab-case key (<c>ai-pages</c>), unique in the product. Stored with the plans and counters.</summary>
    string Key { get; }

    /// <summary>The English name shown to root and the tenant (<c>AI pages</c>).</summary>
    string Name { get; }

    /// <summary>Whether reaching the limit refuses the action or counts as extra usage.</summary>
    UsageLimitMode Mode { get; }

    /// <summary>The limit when a tenant's plan sets none; <c>null</c> is unlimited. Choose a safe one: it is what a forgotten plan gives away.</summary>
    long? DefaultLimit { get; }
}

/// <summary>
/// A meter whose usage is what exists right now (members, items, bytes stored), read from the product's own data.
/// Gauges are never consumed: check with <see cref="IUsageLimits.EnsureWithinLimitAsync"/> before adding.
/// </summary>
public interface IUsageGauge : IUsageMeter
{
    /// <summary>The tenant's current amount. Called inside the tenant's scope.</summary>
    ValueTask<long> GetCurrentAsync(string tenantId, CancellationToken cancellationToken = default);
}
