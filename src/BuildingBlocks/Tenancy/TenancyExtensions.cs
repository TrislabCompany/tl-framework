using System.ComponentModel.DataAnnotations;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.Stores;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Trislab.Framework.Tenancy;

/// <summary>
/// Registers tenancy for a host: how the tenant of a request is resolved, and the event, outbox and job tenant
/// scopes, in single-tenant or multi-tenant mode.
/// </summary>
public static class TenancyExtensions
{
    /// <summary>
    /// Registers tenancy. Call it once, before <c>AddTrislabPlatform</c> and <c>AddModules</c>. The mode comes from
    /// configuration (<c>TenancyOptions:Mode</c>, default multi-tenant) unless <paramref name="configure"/> picks one
    /// with <see cref="TenancyBuilder.UseSingleTenant"/> or <see cref="TenancyBuilder.UseMultitenant"/>.
    /// </summary>
    /// <remarks>
    /// Multi-tenant: the tenant is read from the sign-in claim, a <c>tenant</c> header or a query value, then looked
    /// up in the tenant cache and the catalog store that the Multitenancy module adds.
    /// Single-tenant: one fixed tenant from configuration, always resolved, with no claim, header or catalog; the
    /// quota service is the no-op one.
    /// </remarks>
    public static IHostApplicationBuilder AddTrislabTenancy(
        this IHostApplicationBuilder builder,
        Action<TenancyBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration.GetSection(TenancyOptions.SectionName).Get<TenancyOptions>() ?? new TenancyOptions();
        if (configure is not null)
        {
            configure(new TenancyBuilder(options));

            // Publish the final choice as configuration, so every reader of the mode (the module loader, the
            // quota and storage registration, the pipeline) sees what the code chose.
            builder.Configuration.AddInMemoryCollection(ToConfiguration(options));
        }

        var errors = options.Validate(new ValidationContext(options))
            .Select(r => r.ErrorMessage)
            .ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid tenancy settings ({TenancyOptions.SectionName}, see docs/configuration.md): {string.Join(" ", errors)}");
        }

        builder.Services.AddOptions<TenancyOptions>().BindConfiguration(TenancyOptions.SectionName);

        // Singleton: the buffer survives the request scope that calls Store(...), so a background seed scope can
        // still TryConsume(...). Identity's seeder reads it in both modes; only multi-tenant sign-up stores into it.
        builder.Services.TryAddSingleton<ITenantInitialPasswordBuffer, TenantInitialPasswordBuffer>();

        if (options.Mode == TenancyMode.SingleTenant)
        {
            AddSingleTenant(builder.Services, options.SingleTenant);
        }
        else
        {
            AddMultitenant(builder.Services);
        }

        return builder;
    }

    /// <summary>
    /// Resolves the tenant of each request. Call it where <c>UseMultiTenant</c> used to go: before
    /// <c>UseTrislabPlatform</c>, so before authentication.
    /// </summary>
    public static IApplicationBuilder UseTrislabTenancy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMultiTenant();
    }

    private static void AddSingleTenant(IServiceCollection services, SingleTenantOptions options)
    {
        var provider = new SingleTenantProvider(options);
        services.AddSingleton<ISingleTenantProvider>(provider);

        // The static strategy resolves the fixed identifier for every request; the in-memory store holds the one
        // tenant. Nothing reads a claim, a header or a database, so a caller cannot choose a tenant.
        services
            .AddMultiTenant<AppTenantInfo>()
            .WithStaticStrategy(provider.Tenant.Identifier)
            .WithInMemoryStore(store => store.Tenants.Add(provider.Tenant));

        services.Replace(ServiceDescriptor.Singleton<IEventTenantScope, SingleTenantEventTenantScope>());

        // The outbox drain keeps its default: one database, no per-tenant targets.
    }

    private static void AddMultitenant(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IEventTenantScope, FinbuckleEventTenantScope>());

        // The outbox dispatcher must visit every database that can hold outbox rows. Without these, a tenant with a
        // dedicated connection string writes rows the dispatcher never polls. Scoped provider: the store is scoped.
        services.Replace(ServiceDescriptor.Singleton<IEventingDrainScope, FinbuckleEventingDrainScope>());
        services.Replace(ServiceDescriptor.Scoped<IEventingDrainTargetProvider, TenantStoreDrainTargetProvider>());

        services
            .AddMultiTenant<AppTenantInfo>(options =>
            {
                options.Events.OnTenantResolveCompleted = async context =>
                {
                    if (context.MultiTenantContext.StoreInfo is null) return;
                    if (context.MultiTenantContext.StoreInfo.StoreType != typeof(DistributedCacheStore<AppTenantInfo>))
                    {
                        var sp = ((HttpContext)context.Context).RequestServices;
                        var distributedStore = sp
                            .GetRequiredService<IEnumerable<IMultiTenantStore<AppTenantInfo>>>()
                            .FirstOrDefault(s => s.GetType() == typeof(DistributedCacheStore<AppTenantInfo>));

                        if (distributedStore is not null)
                        {
                            await distributedStore.AddAsync(context.MultiTenantContext.TenantInfo!).ConfigureAwait(false);
                        }
                    }
                };
            })
            // Strategy chain: first non-null identifier wins (registration order).
            // ClaimStrategy authenticates on its own: UseMultiTenant() runs before UseAuthentication(), so Finbuckle
            // runs the default scheme's handler itself. An authenticated caller resolves to its tenant claim and a
            // `tenant` header cannot move it; the header decides only for anonymous requests (login, refresh).
            // The root-operator override is post-auth middleware in the Multitenancy module.
            .WithClaimStrategy(ClaimConstants.Tenant)
            .WithHeaderStrategy(MultitenancyConstants.Identifier)
            .WithDelegateStrategy(context =>
            {
                if (context is not HttpContext httpContext ||
                    !httpContext.Request.Query.TryGetValue("tenant", out var tenantIdentifier) ||
                    string.IsNullOrEmpty(tenantIdentifier))
                {
                    return Task.FromResult<string?>(null);
                }

                return Task.FromResult<string?>(tenantIdentifier.ToString());
            })
            // The catalog store (the Multitenancy module's) registers after this one, so the cache is asked first.
            .WithDistributedCacheStore(TimeSpan.FromMinutes(60));
    }

    private static Dictionary<string, string?> ToConfiguration(TenancyOptions options)
    {
        const string Section = TenancyOptions.SectionName;
        return new Dictionary<string, string?>
        {
            [$"{Section}:Mode"] = options.Mode.ToString(),
            [$"{Section}:SingleTenant:Id"] = options.SingleTenant.Id,
            [$"{Section}:SingleTenant:Name"] = options.SingleTenant.Name,
            [$"{Section}:SingleTenant:Country"] = options.SingleTenant.Country,
            [$"{Section}:SingleTenant:AdminEmail"] = options.SingleTenant.AdminEmail,
        };
    }
}
