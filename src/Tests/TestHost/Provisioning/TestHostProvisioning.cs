using Amazon.S3;
using Amazon.S3.Model;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Tenancy;
using Trislab.Modules.Identity.Authorization;
using Trislab.Modules.Multitenancy.Data;

namespace TestHost.Provisioning;

/// <summary>
/// What a product's migrator does before its API serves: migrate every module's database, seed it and sync
/// the role permissions, for the root tenant in multi-tenant mode or the fixed tenant in single-tenant mode.
/// The integration tests call it from their factories; a standalone run calls it at startup
/// (<c>TestHost:Provision</c>).
/// </summary>
internal static class TestHostProvisioning
{
    public static async Task ProvisionAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var mode = TenancyOptions.ReadMode(provider.GetRequiredService<IConfiguration>());

        AppTenantInfo tenant;
        if (mode == TenancyMode.SingleTenant)
        {
            // No catalog: the fixed tenant comes from the provider.
            tenant = provider.GetRequiredService<ISingleTenantProvider>().Tenant;
        }
        else
        {
            // The tenant catalog first, then the root tenant.
            var tenantDbContext = provider.GetRequiredService<TenantDbContext>();
            await tenantDbContext.Database.MigrateAsync(cancellationToken);

            var rootTenant = await tenantDbContext.TenantInfo.FindAsync([MultitenancyConstants.Root.Id], cancellationToken);
            if (rootTenant is null)
            {
                rootTenant = new AppTenantInfo(
                    MultitenancyConstants.Root.Id,
                    MultitenancyConstants.Root.Name,
                    string.Empty,
                    MultitenancyConstants.Root.EmailAddress,
                    issuer: MultitenancyConstants.Root.Issuer);
                rootTenant.SetValidity(DateTime.UtcNow.AddYears(1));
                await tenantDbContext.TenantInfo.AddAsync(rootTenant, cancellationToken);
                await tenantDbContext.SaveChangesAsync(cancellationToken);
            }

            tenant = rootTenant;
        }

        provider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        // Migrate every module (identity, audit, webhooks, the test module, ...), then seed them all.
        foreach (var init in provider.GetServices<IDbInitializer>())
        {
            await init.MigrateAsync(cancellationToken);
        }

        foreach (var init in provider.GetServices<IDbInitializer>())
        {
            await init.SeedAsync(cancellationToken);
        }

        // The role-permission syncer, through the production code path.
        await provider.GetRequiredService<RolePermissionSyncer>().SyncAsync(cancellationToken);
    }

    /// <summary>Creates the configured S3 bucket when it is missing. A standalone run needs it; the tests create their own.</summary>
    public static async Task EnsureBucketAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetRequiredService<IConfiguration>();
        var bucket = configuration["Storage:S3:Bucket"];
        if (!string.Equals(configuration["Storage:Provider"], "s3", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(bucket))
        {
            return;
        }

        var s3 = services.GetRequiredService<IAmazonS3>();
        try
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Already there.
        }
    }
}
