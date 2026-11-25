using Asp.Versioning;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Trislab.Framework.Eventing;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Multitenancy.Contracts;
using Trislab.Modules.Multitenancy.Data;
using Trislab.Modules.Multitenancy.Features.v1.AdjustTenantValidity;
using Trislab.Modules.Multitenancy.Features.v1.ChangeTenantActivation;
using Trislab.Modules.Multitenancy.Features.v1.CreateTenant;
using Trislab.Modules.Multitenancy.Features.v1.GetMyTenantStatus;
using Trislab.Modules.Multitenancy.Features.v1.GetTenantMigrations;
using Trislab.Modules.Multitenancy.Features.v1.GetTenants;
using Trislab.Modules.Multitenancy.Features.v1.GetTenantStatus;
using Trislab.Modules.Multitenancy.Features.v1.GetTenantTheme;
using Trislab.Modules.Multitenancy.Features.v1.ResetTenantTheme;
using Trislab.Modules.Multitenancy.Features.v1.TenantProvisioning.GetTenantProvisioningStatus;
using Trislab.Modules.Multitenancy.Features.v1.TenantProvisioning.RetryTenantProvisioning;
using Trislab.Modules.Multitenancy.Features.v1.RenewTenant;
using Trislab.Modules.Multitenancy.Features.v1.UpdateTenantTheme;
using Trislab.Modules.Multitenancy.Localization;
using Trislab.Modules.Multitenancy.Provisioning;
using Trislab.Modules.Multitenancy.Services;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Trislab.Modules.Multitenancy;

public sealed class MultitenancyModule : IModule
{
    /// <summary>The tenant catalog, sign-up, lifecycle and operator endpoints need many tenants.</summary>
    public TenancyModes SupportedTenancyModes => TenancyModes.Multitenant;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Trislab.Framework.Shared.Constants.PermissionConstants.Register(
            Trislab.Modules.Multitenancy.Contracts.Authorization.MultitenancyPermissions.All);

        builder.Services.Configure<TenantBillingOptions>(
            builder.Configuration.GetSection(TenantBillingOptions.SectionName));

        builder.Services.AddScoped<ITenantService, TenantService>();
        builder.Services.AddScoped<ITenantThemeService, TenantThemeService>();
        builder.Services.AddTransient<IConnectionStringValidator, ConnectionStringValidator>();
        builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        builder.Services.AddTransient<TenantProvisioningJob>();
        builder.Services.AddTransient<TenantExpiryScanJob>();

        // ITenantInitialPasswordBuffer (the tenant admin password handed to the seed step) is registered by the
        // Tenancy building block in both modes: Identity's seeder needs it with or without this module.

        builder.Services.AddTrislabDbContext<TenantDbContext>(TenantDbContext.Schema);

        // Tenant resolution, the strategy chain, the tenant cache and the event, outbox and job tenant scopes are the
        // Tenancy building block's (AddTrislabTenancy). This module adds the catalog behind them: the one store the
        // tenant manager asks after the cache.
        new MultiTenantBuilder<AppTenantInfo, string>(builder.Services)
            .WithEFCoreStore<TenantDbContext, AppTenantInfo, string>();

        // Tenant-expiry e-mails to the tenant admin. This module owns the events, so it sends the mail
        // (through the Mailing building block); Notifications then works without a tenant catalog.
        builder.Services.AddIntegrationEventHandlers(typeof(MultitenancyModule).Assembly);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TenantDbContext>(
                name: "db:multitenancy",
                failureStatus: HealthStatus.Unhealthy)
            .AddCheck<TenantMigrationsHealthCheck>(
                name: "db:tenants-migrations",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // ── Root-operator header override ──────────────────────────────
        // A "root"-claim caller scopes one request to another tenant via the `tenant` header. The claim strategy has
        // already resolved that caller to root, so the switch happens here, post-auth. Gated on claim==root +
        // header set != root + target exists.
        app.Use(async (ctx, next) =>
        {
            IDisposable? rootOverride = null;
            var callerTenant = ctx.User?.FindFirstValue(ClaimConstants.Tenant);
            if (string.Equals(callerTenant, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
            {
                var headerValue = ctx.Request.Headers[MultitenancyConstants.Identifier].FirstOrDefault();
                if (!string.IsNullOrEmpty(headerValue) &&
                    !string.Equals(headerValue, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
                {
                    var store = ctx.RequestServices.GetRequiredService<IMultiTenantStore<AppTenantInfo, string>>();
                    var target = await store.GetAsync(headerValue).ConfigureAwait(false);
                    if (target is not null)
                    {
                        // For the rest of the request; the root tenant is back once it ends.
                        rootOverride = ctx.RequestServices.GetRequiredService<ITenantAccessor>().Use(target);
                    }
                }
            }
            try
            {
                await next(ctx).ConfigureAwait(false);
            }
            finally
            {
                rootOverride?.Dispose();
            }
        });

        // ── Deactivated-tenant guard ───────────────────────────────────
        // Finbuckle resolves inactive tenants normally, so this post-auth guard rejects any request (incl.
        // anonymous login/refresh) with a non-root inactive tenant; root operators are exempt.
        app.Use(async (ctx, next) =>
        {
            var callerTenant = ctx.User?.FindFirstValue(ClaimConstants.Tenant);
            var isOperator = string.Equals(callerTenant, MultitenancyConstants.Root.Id, StringComparison.Ordinal);
            if (!isOperator)
            {
                var accessor = ctx.RequestServices.GetRequiredService<ITenantAccessor>();
                var tenant = accessor.TenantInfo;

                // An authenticated request normally resolves through the claim strategy; if nothing resolved,
                // fall back to the caller's claim.
                if (tenant is null && !string.IsNullOrEmpty(callerTenant))
                {
                    var store = ctx.RequestServices.GetRequiredService<IMultiTenantStore<AppTenantInfo, string>>();
                    tenant = await store.GetAsync(callerTenant).ConfigureAwait(false);
                }

                if (tenant is not null &&
                    !string.Equals(tenant.Id, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
                {
                    if (!tenant.IsActive)
                    {
                        throw new ForbiddenException("This tenant has been deactivated. Contact your administrator.")
                            .Localized<MultitenancyResources>("Multitenancy.TenantDeactivated");
                    }

                    // Expiry is enforced on every request (not just at login) with a grace period:
                    // a tenant past ValidUpto still works until ValidUpto + grace, then is hard-blocked.
                    var graceDays = ctx.RequestServices
                        .GetRequiredService<IOptions<TenantBillingOptions>>().Value.GracePeriodDays;
                    var nowUtc = ctx.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                    var graceEndsUtc = tenant.ValidUpto.AddDays(graceDays);
                    if (nowUtc > graceEndsUtc)
                    {
                        throw new ForbiddenException("This tenant's subscription has expired. Please renew to continue.")
                            .Localized<MultitenancyResources>("Multitenancy.TenantSubscriptionExpired");
                    }

                    // Inside the grace period: surface days-left so clients can warn. Set via OnStarting so
                    // the header survives even when an exception handler rewrites the response.
                    if (nowUtc > tenant.ValidUpto)
                    {
                        var daysLeft = (int)Math.Ceiling((graceEndsUtc - nowUtc).TotalDays);
                        var headerValue = daysLeft.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        ctx.Response.OnStarting(static state =>
                        {
                            var (response, value) = ((HttpResponse, string))state;
                            response.Headers["X-Subscription-Grace"] = value;
                            return Task.CompletedTask;
                        }, (ctx.Response, headerValue));
                    }
                }
            }

            await next(ctx).ConfigureAwait(false);
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints.MapGroup("api/v{version:apiVersion}/tenants")
            .WithTags("Tenants")
            .WithApiVersionSet(versionSet);
        ChangeTenantActivationEndpoint.Map(group);
        GetTenantsEndpoint.Map(group);
        RenewTenantEndpoint.Map(group);
        AdjustTenantValidityEndpoint.Map(group);
        CreateTenantEndpoint.Map(group);
        GetTenantStatusEndpoint.Map(group);
        GetMyTenantStatusEndpoint.Map(group);
        GetTenantProvisioningStatusEndpoint.Map(group);
        RetryTenantProvisioningEndpoint.Map(group);
        TenantMigrationsEndpoint.Map(group);

        // Theme endpoints
        GetTenantThemeEndpoint.Map(group);
        UpdateTenantThemeEndpoint.Map(group);
        ResetTenantThemeEndpoint.Map(group);

        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        if (jobManager is not null)
        {
            // Scan tenants daily at 02:00 UTC; publishes nearing-expiry / entered-grace / expired notices.
            jobManager.AddOrUpdate(
                "tenant-expiry-scan",
                Job.FromExpression<TenantExpiryScanJob>(j => j.RunAsync(CancellationToken.None)),
                "0 2 * * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}