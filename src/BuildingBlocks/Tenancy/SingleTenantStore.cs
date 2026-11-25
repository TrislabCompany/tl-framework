using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// The tenant store of a single-tenant host: the one fixed tenant from configuration, read-only. A caller can
/// neither add a tenant nor change or remove this one, so no request reaches another tenant.
/// </summary>
internal sealed class SingleTenantStore(AppTenantInfo tenant) : IMultiTenantStore<AppTenantInfo, string>
{
    public Task<AppTenantInfo?> GetAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Equals(id, tenant.Id, StringComparison.Ordinal) ? tenant : null);

    public Task<AppTenantInfo?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Equals(identifier, tenant.Identifier, StringComparison.OrdinalIgnoreCase) ? tenant : null);

    public Task<IEnumerable<AppTenantInfo>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AppTenantInfo>>([tenant]);

    public Task<IEnumerable<AppTenantInfo>> GetAllAsync(int take, int skip, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<AppTenantInfo>>(skip == 0 && take > 0 ? [tenant] : []);

    public Task<bool> AddAsync(AppTenantInfo tenantInfo, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> UpdateAsync(AppTenantInfo tenantInfo, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> RemoveByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
