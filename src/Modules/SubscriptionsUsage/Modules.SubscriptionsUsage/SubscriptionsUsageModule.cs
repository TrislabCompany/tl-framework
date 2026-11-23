using Trislab.Framework.Shared.Multitenancy;
using Asp.Versioning;
using Trislab.Framework.Eventing;
using Trislab.Framework.Persistence;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GenerateInvoices;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GetInvoiceById;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GetInvoices;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GetInvoicePdf;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GetMyInvoices;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.IssueInvoice;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.MarkInvoicePaid;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.VoidInvoice;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.CreatePlan;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.GetPlans;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.UpdatePlan;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Subscriptions.AssignSubscription;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Subscriptions.GetSubscription;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Usage.CaptureUsageSnapshots;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Usage.GetUsageSnapshots;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.ApproveTopupRequest;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.CreateTopupRequest;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.GetMyTopupRequests;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.GetMyWallet;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.GetTopupRequests;
using Trislab.Modules.SubscriptionsUsage.Features.v1.Wallets.RejectTopupRequest;
using Trislab.Modules.SubscriptionsUsage.Services;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: TrislabModule(typeof(Trislab.Modules.SubscriptionsUsage.SubscriptionsUsageModule), 500)]

namespace Trislab.Modules.SubscriptionsUsage;

public sealed class SubscriptionsUsageModule : IModule
{
    /// <summary>Subscriptions, plans and invoices belong to the tenants of a tenant catalog.</summary>
    public TenancyModes SupportedTenancyModes => TenancyModes.Multitenant;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Trislab.Framework.Shared.Constants.PermissionConstants.Register(
            Trislab.Modules.SubscriptionsUsage.Contracts.Authorization.BillingPermissions.All);

        builder.Services.AddTrislabDbContext<BillingDbContext>(BillingDbContext.Schema);
        builder.Services.AddScoped<IDbInitializer, BillingDbInitializer>();
        builder.Services.AddScoped<IUsageReporter, UsageReporter>();
        builder.Services.AddScoped<IBillingService, BillingService>();
        builder.Services.AddSingleton<IInvoicePdfRenderer, InvoicePdfRenderer>();

        // React to tenant create/renew events (Multitenancy.Contracts) to drive subscriptions + invoices.
        builder.Services.AddIntegrationEventHandlers(typeof(SubscriptionsUsageModule).Assembly);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<BillingDbContext>(
                name: "db:billing",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
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
            .MapGroup("api/v{version:apiVersion}/billing")
            .WithTags("Billing")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapGetPlansEndpoint();
        group.MapCreatePlanEndpoint();
        group.MapUpdatePlanEndpoint();

        group.MapGetSubscriptionEndpoint();
        group.MapGetMySubscriptionEndpoint();
        group.MapAssignSubscriptionEndpoint();

        group.MapGetInvoicesEndpoint();
        group.MapGetMyInvoicesEndpoint();
        group.MapGetInvoiceByIdEndpoint();
        group.MapGetInvoicePdfEndpoint();
        group.MapGenerateInvoicesEndpoint();
        group.MapIssueInvoiceEndpoint();
        group.MapMarkInvoicePaidEndpoint();
        group.MapVoidInvoiceEndpoint();

        group.MapGetUsageSnapshotsEndpoint();
        group.MapCaptureUsageSnapshotsEndpoint();

        group.MapGetMyWalletEndpoint();
        group.MapCreateTopupRequestEndpoint();
        group.MapGetMyTopupRequestsEndpoint();

        group.MapGetTopupRequestsEndpoint();
        group.MapApproveTopupRequestEndpoint();
        group.MapRejectTopupRequestEndpoint();

        var jobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
        if (jobManager is not null)
        {
            // Fire at 00:05 UTC on the 1st of every month; the job bills the previous period.
            jobManager.AddOrUpdate(
                "billing-monthly-invoices",
                Job.FromExpression<MonthlyInvoiceJob>(j => j.RunAsync(CancellationToken.None)),
                "5 0 1 * *",
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}
