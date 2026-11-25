using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Multitenancy.Contracts.Authorization;
using Trislab.Modules.Multitenancy.Contracts.Dtos;
using Trislab.Modules.Multitenancy.Contracts.v1.GetTenantMigrations;
using Trislab.Modules.Multitenancy.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Trislab.Modules.Multitenancy.Features.v1.GetTenantMigrations;

public sealed class GetTenantMigrationsQueryHandler
    : IQueryHandler<GetTenantMigrationsQuery, IReadOnlyCollection<TenantMigrationStatusDto>>
{
    private readonly IMultiTenantStore<AppTenantInfo, string> _tenantStore;
    private readonly IServiceScopeFactory _scopeFactory;

    public GetTenantMigrationsQueryHandler(
        IMultiTenantStore<AppTenantInfo, string> tenantStore,
        IServiceScopeFactory scopeFactory)
    {
        _tenantStore = tenantStore;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask<IReadOnlyCollection<TenantMigrationStatusDto>> Handle(
        GetTenantMigrationsQuery query,
        CancellationToken cancellationToken)
    {
        var tenants = await _tenantStore.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var tenantMigrationStatuses = new List<TenantMigrationStatusDto>();

        foreach (var tenant in tenants)
        {
            var tenantStatus = new TenantMigrationStatusDto
            {
                TenantId = tenant.Id,
                Name = tenant.Name!,
                IsActive = tenant.IsActive,
                ValidUpto = tenant.ValidUpto
            };

            try
            {
                using IServiceScope tenantScope = _scopeFactory.CreateScope();

                using var tenantUse = tenantScope.ServiceProvider.GetRequiredService<ITenantAccessor>().Use(tenant);

                var dbContext = tenantScope.ServiceProvider.GetRequiredService<TenantDbContext>();

                var appliedMigrations = await dbContext.Database
                    .GetAppliedMigrationsAsync(cancellationToken)
                    .ConfigureAwait(false);

                var pendingMigrations = await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken)
                    .ConfigureAwait(false);

                tenantStatus.Provider = dbContext.Database.ProviderName;
                tenantStatus.LastAppliedMigration = appliedMigrations.LastOrDefault();
                tenantStatus.PendingMigrations = pendingMigrations.ToArray();
                tenantStatus.HasPendingMigrations = tenantStatus.PendingMigrations.Count > 0;
            }
            // Per-tenant failure must not stop reporting on other tenants
            catch (Exception ex)
            {
                tenantStatus.Error = ex.Message;
            }

            tenantMigrationStatuses.Add(tenantStatus);
        }

        return tenantMigrationStatuses;
    }
}