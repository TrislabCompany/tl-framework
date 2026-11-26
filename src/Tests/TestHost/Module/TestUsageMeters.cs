using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.SubscriptionsUsage.Contracts;
using Trislab.Modules.SubscriptionsUsage.Contracts.Limits;

namespace TestHost;

/// <summary>A counter with a hard limit, like AI pages a product refuses past the plan.</summary>
internal sealed class TestPagesMeter : IUsageMeter
{
    public const string MeterKey = "test-pages";
    public string Key => MeterKey;
    public string Name => "Test pages";
    public UsageLimitMode Mode => UsageLimitMode.HardLimit;
    public long? DefaultLimit => 3;
}

/// <summary>A counter whose units above the limit are extra usage, never refused.</summary>
internal sealed class TestExtraMeter : IUsageMeter
{
    public const string MeterKey = "test-extra";
    public string Key => MeterKey;
    public string Name => "Test extra units";
    public UsageLimitMode Mode => UsageLimitMode.ExtraUsage;
    public long? DefaultLimit => 2;
}

/// <summary>A gauge: the tenant's <see cref="TestItem"/> rows, like members or items a product counts itself.</summary>
internal sealed class TestItemsMeter(TestDbContext dbContext) : IUsageGauge
{
    public const string MeterKey = "test-items";
    public string Key => MeterKey;
    public string Name => "Test items";
    public UsageLimitMode Mode => UsageLimitMode.HardLimit;
    public long? DefaultLimit => 2;

    public async ValueTask<long> GetCurrentAsync(string tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.Items.LongCountAsync(cancellationToken).ConfigureAwait(false);
}

internal static class TestUsageEndpoints
{
    /// <summary>
    /// POST /limits/{meterKey}/consume adds units to a counter; POST /items adds an item after checking the gauge.
    /// <see cref="IUsageLimits"/> comes from SubscriptionsUsage, which only a multi-tenant host registers, so it is
    /// marked <c>[FromServices]</c>: in single-tenant mode the endpoint still maps, and is never called.
    /// </summary>
    public static RouteGroupBuilder MapTestUsageEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/limits/{meterKey}/consume", async (string meterKey, long? amount, [FromServices] IUsageLimits limits, CancellationToken ct)
            => Results.Ok(await limits.ConsumeAsync(meterKey, amount ?? 1, ct).ConfigureAwait(false)));

        group.MapPost("/items", async (CreateItemRequest request, [FromServices] IUsageLimits limits, TestDbContext dbContext, CancellationToken ct) =>
        {
            await limits.EnsureWithinLimitAsync(TestItemsMeter.MeterKey, 1, ct).ConfigureAwait(false);
            var item = TestItem.Create(request.Name);
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            return Results.Ok(item.Id);
        });

        return group;
    }

    internal sealed record CreateItemRequest(string Name);
}
