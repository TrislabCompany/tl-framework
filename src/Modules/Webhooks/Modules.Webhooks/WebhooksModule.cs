using Trislab.Framework.Shared.Multitenancy;
using Asp.Versioning;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Web.HttpResilience;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Webhooks.Contracts.Authorization;
using Trislab.Modules.Webhooks.Data;
using Trislab.Modules.Webhooks.Features.v1.CreateWebhookSubscription;
using Trislab.Modules.Webhooks.Features.v1.DeleteWebhookSubscription;
using Trislab.Modules.Webhooks.Features.v1.GetWebhookDeliveries;
using Trislab.Modules.Webhooks.Features.v1.GetWebhookSubscriptions;
using Trislab.Modules.Webhooks.Features.v1.TestWebhookSubscription;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Modules.Webhooks.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

[assembly: TrislabModule(typeof(Trislab.Modules.Webhooks.WebhooksModule), 400)]

namespace Trislab.Modules.Webhooks;

public sealed class WebhooksModule : IModule
{
    /// <summary>Works with and without a tenant catalog; data is stamped with the tenant in both modes.</summary>
    public TenancyModes SupportedTenancyModes => TenancyModes.All;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(WebhooksPermissions.All);

        builder.Services.AddTrislabDbContext<WebhookDbContext>(WebhookDbContext.Schema);
        builder.Services.AddScoped<IDbInitializer, WebhookDbInitializer>();
        builder.Services.AddSingleton<IWebhookSecretProtector, WebhookSecretProtector>();
        builder.Services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();
        builder.Services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
        builder.Services.AddScoped<WebhookDispatchJob>();

        // Open-generic integration-event bridge — every published IIntegrationEvent fans out to
        // matching tenant webhook subscriptions; DI materializes closed handler types per event.
        builder.Services.AddScoped(
            typeof(IIntegrationEventHandler<>),
            typeof(WebhookFanoutHandler<>));

        builder.Services.AddHttpClient("Webhooks")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Untrusted tenant-supplied destination: never follow redirects (a 302 could bounce
                // to an internal host) and screen the resolved IP at connect time so DNS-rebinding
                // cannot map a public hostname to an internal address after the create-time check.
                AllowAutoRedirect = false,
                ConnectCallback = WebhookUrlGuard.ConnectAsync,
            })
            .AddTrislabResilience(builder.Configuration);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<WebhookDbContext>(
                name: "db:webhooks",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(Microsoft.AspNetCore.Builder.IApplicationBuilder app)
    {
        // No custom middleware needed
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/webhooks")
            .WithTags("Webhooks")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapCreateWebhookSubscriptionEndpoint();
        group.MapDeleteWebhookSubscriptionEndpoint();
        group.MapGetWebhookSubscriptionsEndpoint();
        group.MapGetWebhookDeliveriesEndpoint();
        group.MapTestWebhookSubscriptionEndpoint();
    }
}
