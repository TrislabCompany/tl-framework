using Finbuckle.MultiTenant;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// Multi-tenant-mode <see cref="IEventTenantScope"/>, backed by Finbuckle. Installs the ambient tenant context
/// (an AsyncLocal in Finbuckle) for the duration of an integration-event dispatch so that
/// handler DbContexts resolved afterwards capture a real <c>TenantInfo</c> instead of the
/// null one a background scope would otherwise carry.
///
/// <see cref="ExecuteAsync"/> resolves the full tenant (including a dedicated connection string)
/// before the action constructs consumers, in the same async flow.
/// </summary>
public sealed class FinbuckleEventTenantScope : IEventTenantScope
{
    private readonly ITenantAccessor _tenants;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinbuckleEventTenantScope> _logger;

    public FinbuckleEventTenantScope(
        ITenantAccessor tenants,
        IServiceScopeFactory scopeFactory,
        ILogger<FinbuckleEventTenantScope> logger)
    {
        _tenants = tenants;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IDisposable Begin(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            // Global events: leave the ambient context untouched.
            return NoopScope.Instance;
        }

        return _tenants.Use(new AppTenantInfo(tenantId, tenantId));
    }

    public async Task ExecuteAsync(
        string? tenantId,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var current = _tenants.TenantInfo;
        if (string.IsNullOrWhiteSpace(tenantId) ||
            (string.Equals(current?.Id, tenantId, StringComparison.Ordinal) &&
             !string.IsNullOrWhiteSpace(current?.ConnectionString)))
        {
            await action(ct).ConfigureAwait(false);
            return;
        }

        var tenant = await FindTenantAsync(tenantId, ct).ConfigureAwait(false);
        if (tenant is null || !tenant.IsActive)
        {
            _logger.LogWarning("Event tenant {TenantId} is missing or inactive; dispatching with tenant identity only", tenantId);
            tenant = new AppTenantInfo(tenantId, tenantId);
        }

        using (_tenants.Use(tenant))
        {
            await action(ct).ConfigureAwait(false);
        }
    }

    private async Task<AppTenantInfo?> FindTenantAsync(string tenantId, CancellationToken ct)
    {
        // The tenant manager asks the tenant cache first, then the catalog store, and fills the cache on a miss.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var tenants = scope.ServiceProvider.GetRequiredService<TenantManager<AppTenantInfo, string>>();
        return await tenants.GetAsync(tenantId, ct).ConfigureAwait(false);
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }
}
