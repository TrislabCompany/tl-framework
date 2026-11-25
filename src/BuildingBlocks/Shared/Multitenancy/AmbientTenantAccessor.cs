using Finbuckle.MultiTenant.Abstractions;

namespace Trislab.Framework.Shared.Multitenancy;

/// <summary>
/// <see cref="ITenantAccessor"/> over Finbuckle's ambient tenant context (an <see cref="AsyncLocal{T}"/>).
/// Finbuckle 11 lets a scope's tenant be set only once and has no way back to an outer tenant, while a job that
/// walks every tenant, or an event dispatched inside a request, needs one tenant after another and the outer one
/// back afterwards. <see cref="Use"/> starts a new ambient scope and restores the previous one on dispose.
/// Registered as a singleton by <c>AddTrislabTenancy</c>, next to Finbuckle's own singleton context.
/// </summary>
public sealed class AmbientTenantAccessor : ITenantAccessor
{
    private readonly AmbientTenantContext<AppTenantInfo, string> _ambient;

    public AmbientTenantAccessor(ITenantContext<AppTenantInfo, string> context)
    {
        _ambient = context as AmbientTenantContext<AppTenantInfo, string>
            ?? throw new InvalidOperationException(
                $"The tenant context is {context?.GetType().Name ?? "missing"}; the Tenancy building block needs Finbuckle's "
                + $"{nameof(AmbientTenantContext<AppTenantInfo, string>)}, registered by AddTrislabTenancy.");
    }

    /// <inheritdoc />
    // Finbuckle throws when there is no ambient scope; outside one there is simply no tenant.
    public AppTenantInfo? TenantInfo => _ambient.HasScope ? _ambient.TenantInfo : null;

    /// <inheritdoc />
    public bool IsResolved => TenantInfo is not null;

    /// <inheritdoc />
    public IDisposable Use(AppTenantInfo? tenant)
    {
        var previous = _ambient.TenantContext.Value;
        _ambient.BeginScope();
        if (tenant is not null)
        {
            _ambient.TenantInfo = tenant;
        }

        return new Restore(() => _ambient.TenantContext.Value = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
