using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Multitenancy.Contracts;
using Trislab.Modules.Multitenancy.Localization;
using Trislab.Modules.Multitenancy.Services;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Multitenancy.Provisioning;

public sealed class TenantProvisioningJob
{
    private readonly ITenantProvisioningService _provisioningService;
    private readonly IMultiTenantStore<AppTenantInfo, string> _tenantStore;
    private readonly ITenantAccessor _tenants;
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantProvisioningJob> _logger;

    public TenantProvisioningJob(
        ITenantProvisioningService provisioningService,
        IMultiTenantStore<AppTenantInfo, string> tenantStore,
        ITenantAccessor tenants,
        ITenantService tenantService,
        ILogger<TenantProvisioningJob> logger)
    {
        _provisioningService = provisioningService;
        _tenantStore = tenantStore;
        _tenants = tenants;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task RunAsync(string tenantId, string correlationId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantStore.GetAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Tenant {tenantId} not found during provisioning.")
                .Localized<MultitenancyResources>("Multitenancy.TenantNotFoundDuringProvisioning", tenantId);

        var currentStep = TenantProvisioningStepName.Database;
        try
        {
            var runDatabase = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);

            using var tenantUse = _tenants.Use(tenant);

            if (runDatabase)
            {
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            currentStep = TenantProvisioningStepName.Migrations;
            var runMigrations = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            if (runMigrations)
            {
                await _tenantService.MigrateTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            currentStep = TenantProvisioningStepName.Seeding;
            var runSeeding = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            if (runSeeding)
            {
                await _tenantService.SeedTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            currentStep = TenantProvisioningStepName.CacheWarm;
            var runCacheWarm = await _provisioningService.MarkRunningAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            if (runCacheWarm)
            {
                await _provisioningService.MarkStepCompletedAsync(tenantId, correlationId, currentStep, cancellationToken).ConfigureAwait(false);
            }

            await _provisioningService.MarkCompletedAsync(tenantId, correlationId, cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Provisioned tenant {TenantId} correlation {CorrelationId}", tenantId, correlationId);
            }
        }
        // Broad catch is intentional: any provisioning failure must be recorded in the provisioning
        // log before re-throwing so the job framework can handle retry/dead-letter.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning failed for tenant {TenantId}", tenantId);
            await _provisioningService.MarkFailedAsync(tenantId, correlationId, currentStep, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}