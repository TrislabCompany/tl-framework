using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Webhooks.Data;
using Trislab.Modules.Webhooks.Domain;
using Trislab.Modules.Webhooks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Webhooks.Tests.Services;

public sealed class WebhookFanoutHandlerTests
{
    private const string TenantId = "tenant-acme";
    private const string EventType = nameof(FakeIntegrationEvent);

    private readonly TestTenantAccessor _tenantAccessor = new();
    private readonly string _databaseName = $"webhooks-{Guid.NewGuid():N}";
    private readonly IWebhookDispatcher _dispatcher = Substitute.For<IWebhookDispatcher>();
    private readonly IEventSerializer _serializer = Substitute.For<IEventSerializer>();
    private readonly ILogger<WebhookFanoutHandler<FakeIntegrationEvent>> _logger =
        Substitute.For<ILogger<WebhookFanoutHandler<FakeIntegrationEvent>>>();

    public WebhookFanoutHandlerTests()
    {
        _serializer.Serialize(Arg.Any<IIntegrationEvent>()).Returns("{\"serialized\":true}");

        // A context takes its tenant when it is built, so the seeding context needs the tenant first.
        SetTenant(TenantId);
    }

    #region Happy Path

    [Fact]
    public async Task HandleAsync_Should_Enqueue_Delivery_When_Subscription_Matches_Exact_Event()
    {
        await using var db = CreateContext();
        Guid subId = await SeedSubscriptionAsync(db, [EventType], isActive: true);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));

        await _dispatcher.Received(1).EnqueueAsync(
            TenantId, subId, EventType, "{\"serialized\":true}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Should_Enqueue_Delivery_When_Subscription_Uses_Wildcard()
    {
        await using var db = CreateContext();
        Guid subId = await SeedSubscriptionAsync(db, ["*"], isActive: true);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));

        await _dispatcher.Received(1).EnqueueAsync(
            TenantId, subId, EventType, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Should_Enqueue_For_Each_Matching_Subscription()
    {
        await using var db = CreateContext();
        await SeedSubscriptionAsync(db, [EventType], isActive: true);
        await SeedSubscriptionAsync(db, ["*"], isActive: true);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));

        await _dispatcher.Received(2).EnqueueAsync(
            TenantId, Arg.Any<Guid>(), EventType, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Should_ReadTheEventsTenant_When_TheCallerHoldsAnotherTenant()
    {
        await using var db = CreateContext();
        Guid subId = await SeedSubscriptionAsync(db, [EventType], isActive: true);
        var handler = CreateHandler();

        // A background pump may still hold the tenant of the previous event, or none.
        SetTenant("tenant-other");
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));
        _tenantAccessor.TenantInfo.ShouldNotBeNull().Id.ShouldBe("tenant-other", "the caller's tenant comes back");

        SetTenant(null);
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));

        await _dispatcher.Received(2).EnqueueAsync(
            TenantId, subId, EventType, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Skip / No-Match Branches

    [Fact]
    public async Task HandleAsync_Should_Skip_When_Event_TenantId_Is_Null()
    {
        await using var db = CreateContext();
        await SeedSubscriptionAsync(db, [EventType], isActive: true);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId: null));

        await _dispatcher.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default, default!, default!, default);
        _serializer.DidNotReceiveWithAnyArgs().Serialize(default!);
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_When_Event_TenantId_Is_Whitespace()
    {
        await using var db = CreateContext();
        await SeedSubscriptionAsync(db, [EventType], isActive: true);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent("   "));

        await _dispatcher.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default, default!, default!, default);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Enqueue_When_No_Subscription_Matches_Event()
    {
        await using var db = CreateContext();
        await SeedSubscriptionAsync(db, ["some.other.event"], isActive: true);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));

        await _dispatcher.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default, default!, default!, default);
        _serializer.DidNotReceiveWithAnyArgs().Serialize(default!);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Enqueue_When_Matching_Subscription_Is_Inactive()
    {
        await using var db = CreateContext();
        await SeedSubscriptionAsync(db, [EventType], isActive: false);

        var handler = CreateHandler();
        await handler.HandleAsync(new FakeIntegrationEvent(TenantId));

        await _dispatcher.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default, default!, default!, default);
    }

    #endregion

    #region Resilience

    [Fact]
    public async Task HandleAsync_Should_Continue_Fanout_When_One_Enqueue_Throws()
    {
        await using var db = CreateContext();
        Guid first = await SeedSubscriptionAsync(db, [EventType], isActive: true);
        Guid second = await SeedSubscriptionAsync(db, [EventType], isActive: true);

        _dispatcher
            .EnqueueAsync(TenantId, first, EventType, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("transient enqueue failure"));

        var handler = CreateHandler();

        // Must not bubble — a single bad subscription cannot abort fan-out to the rest.
        await Should.NotThrowAsync(async () => await handler.HandleAsync(new FakeIntegrationEvent(TenantId)));

        await _dispatcher.Received(1).EnqueueAsync(
            TenantId, second, EventType, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Should_Throw_When_Event_Is_Null()
    {
        await using var db = CreateContext();
        var handler = CreateHandler();

        await Should.ThrowAsync<ArgumentNullException>(async () => await handler.HandleAsync(null!));
    }

    #endregion

    #region Helpers

    private WebhookFanoutHandler<FakeIntegrationEvent> CreateHandler()
    {
        // The handler opens its own context on the same in-memory database.
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateContext());
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new(scopes, _dispatcher, _serializer, _tenantAccessor, _logger);
    }

    private WebhookDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WebhookDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;

        var settings = Options.Create(new DatabaseOptions());
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");

        return new WebhookDbContext(_tenantAccessor, options, settings, environment);
    }

    private async Task<Guid> SeedSubscriptionAsync(WebhookDbContext db, string[] events, bool isActive)
    {
        // Install tenant context so Finbuckle stamps the seeded row (Overwrite mode)
        // and the handler's tenant-filtered read can see it.
        SetTenant(TenantId);

        WebhookSubscription sub = WebhookSubscription.Create("https://example.com/hook", events, "hash");
        if (!isActive)
        {
            sub.Deactivate();
        }

        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return sub.Id;
    }

    private void SetTenant(string? tenantId) =>
        _tenantAccessor.Set(tenantId is null ? null : new AppTenantInfo(tenantId, tenantId));

    #endregion

    private sealed class TestTenantAccessor : ITenantAccessor
    {
        public AppTenantInfo? TenantInfo { get; private set; }

        public bool IsResolved => TenantInfo is not null;

        public void Set(AppTenantInfo? tenant) => TenantInfo = tenant;

        public IDisposable Use(AppTenantInfo? tenant)
        {
            var previous = TenantInfo;
            TenantInfo = tenant;
            return new Restore(() => TenantInfo = previous);
        }

        private sealed class Restore(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }
}

public sealed record FakeIntegrationEvent(string? TenantId) : IIntegrationEvent
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
    public string CorrelationId { get; } = Guid.CreateVersion7().ToString();
    public string Source { get; } = "Tests";
}
