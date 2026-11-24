using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TestHost;
using TestHost.Provisioning;
using Trislab.Framework.Eventing;
using Trislab.Framework.Shared.Health;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Tenancy;
using Trislab.Framework.Web;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Auditing;
using Trislab.Modules.Identity;
using Trislab.Modules.Multitenancy;
using Trislab.Modules.Notifications;
using Trislab.Modules.SubscriptionsUsage;
using Trislab.Modules.Webhooks;

var builder = WebApplication.CreateBuilder(args);

// The settings are plain configuration (appsettings, environment variables, command line). The platform's
// variable mapping (DATABASE_URL, S3_*) belongs to the product hosts, not to a test host.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies = [
        typeof(Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration.GenerateTokenCommand),
        typeof(Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration.GenerateTokenCommandHandler),
        typeof(Trislab.Modules.Multitenancy.Contracts.v1.GetTenantStatus.GetTenantStatusQuery),
        typeof(Trislab.Modules.Multitenancy.Features.v1.GetTenantStatus.GetTenantStatusQueryHandler),
        typeof(Trislab.Modules.Auditing.Contracts.AuditEnvelope),
        typeof(Trislab.Modules.Auditing.Persistence.AuditDbContext),
        typeof(Trislab.Modules.Webhooks.Contracts.v1.CreateWebhookSubscription.CreateWebhookSubscriptionCommand),
        typeof(WebhooksModule),
        typeof(Trislab.Modules.SubscriptionsUsage.Contracts.BillingContractsMarker),
        typeof(SubscriptionsUsageModule),
        typeof(Trislab.Modules.InvoiceDocuments.Contracts.InvoiceDocumentsContractsMarker),
        typeof(Trislab.Modules.InvoiceDocuments.InvoiceDocumentsModule),
        typeof(Trislab.Modules.Files.Contracts.v1.Commands.RequestUploadUrlCommand),
        typeof(Trislab.Modules.Files.FilesModule),
        typeof(Trislab.Modules.Notifications.Contracts.v1.Commands.MarkNotificationReadCommand),
        typeof(NotificationsModule)];
});

// Every framework module, and the test module of this host (Module/TestModule.cs).
var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly,
    typeof(Trislab.Modules.Files.FilesModule).Assembly,
    typeof(WebhooksModule).Assembly,
    typeof(SubscriptionsUsageModule).Assembly,
    typeof(Trislab.Modules.InvoiceDocuments.InvoiceDocumentsModule).Assembly,
    typeof(NotificationsModule).Assembly,
    typeof(TestModule).Assembly,
};

// Tenancy first: the mode is multi-tenant unless TenancyOptions:Mode says otherwise. A single-tenant run
// leaves out the multi-tenant-only modules (Multitenancy, SubscriptionsUsage).
builder.AddTrislabTenancy();
moduleAssemblies = ModuleLoader.SelectSupported(TenancyOptions.ReadMode(builder.Configuration), moduleAssemblies);

builder.AddTrislabPlatform(o =>
{
    o.EnableCaching = true;
    o.EnableMailing = true;
    o.EnableJobs = true;
    o.EnableQuotas = true;
    o.EnableSse = true;
    o.EnableRealtime = true;
});

builder.Services.AddEventingCore(builder.Configuration);

builder.AddModules(moduleAssemblies);

var app = builder.Build();

app.UseTrislabTenancy();
app.UseTrislabPlatform(p =>
{
    p.MapModules = true;
    p.ServeStaticFiles = true;
    p.UseQuotas = true;
    p.MapSseEndpoints = true;
    p.MapRealtime = true;
});

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
// The platform's readiness probe skips the details-only checks (file storage), as /api/health/ready does.
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = c => !c.Tags.Contains(HealthCheckTags.DetailsOnly) }).AllowAnonymous();

app.MapGet("/api/health/info", (IConfiguration config) => Results.Ok(new
   {
       name = "TestHost",
       version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
       environment = config["Platform:Environment"],
   }))
   .WithTags("Health")
   .AllowAnonymous();

app.MapGet("/", () => Results.Ok(new { message = "hello world!" }))
   .WithTags("PlayGround")
   .AllowAnonymous();

// A standalone run (compose.yaml, README.md) migrates and seeds itself: nothing else does. The tests
// provision through their factory instead, so the switch is off by default.
if (app.Configuration.GetValue<bool>("TestHost:Provision"))
{
    await TestHostProvisioning.EnsureBucketAsync(app.Services, app.Lifetime.ApplicationStopping);
    await TestHostProvisioning.ProvisionAsync(app.Services, app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
