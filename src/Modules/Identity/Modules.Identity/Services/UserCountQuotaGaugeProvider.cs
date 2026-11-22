using Trislab.Framework.Quota;
using Trislab.Framework.Shared.Quota;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Services;

/// <summary>
/// Reports the live user count for a tenant as a quota gauge. Uses the tenant-scoped
/// <see cref="IdentityDbContext"/>, so the provider only answers for the request's resolved tenant;
/// for any other tenant id we defer to <see cref="UserManager{TUser}"/> with the tenant filter
/// bypassed to avoid cross-tenant leakage of counts.
/// </summary>
internal sealed class UserCountQuotaGaugeProvider : IQuotaGaugeProvider
{
    private readonly UserManager<TrislabUser> _userManager;

    public UserCountQuotaGaugeProvider(UserManager<TrislabUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        _userManager = userManager;
    }

    public QuotaResource Resource => QuotaResource.Users;

    public async ValueTask<long> GetCurrentAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        return await _userManager.Users
            .IgnoreQueryFilters()
            .CountAsync(u => EF.Property<string>(u, "TenantId") == tenantId, ct)
            .ConfigureAwait(false);
    }
}
