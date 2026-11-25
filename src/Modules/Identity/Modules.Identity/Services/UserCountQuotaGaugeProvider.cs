using Trislab.Framework.Quota;
using Trislab.Framework.Shared.Quota;
using Trislab.Modules.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Services;

/// <summary>
/// Reports a tenant's user count as a quota gauge: the number of its memberships, read across tenants
/// with an explicit tenant id, because the gauge is asked for tenants other than the request's.
/// </summary>
internal sealed class UserCountQuotaGaugeProvider : IQuotaGaugeProvider
{
    private readonly IdentityDbContext _db;

    public UserCountQuotaGaugeProvider(IdentityDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public QuotaResource Resource => QuotaResource.Users;

    public async ValueTask<long> GetCurrentAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // Users are global; a tenant's users are its memberships. A deactivated member still counts: the
        // seat stays taken until the administrator removes it.
        return await _db.TenantMemberships
            .IgnoreQueryFilters()
            .CountAsync(m => m.TenantId == tenantId, ct)
            .ConfigureAwait(false);
    }
}
