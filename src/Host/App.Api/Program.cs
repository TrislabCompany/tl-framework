using App.Api.Platform;
using Trislab.Framework.Eventing;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Tenancy;
using Trislab.Framework.Web;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Auditing;
using Trislab.Modules.Identity;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Trislab.Modules.Multitenancy;
using Trislab.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using Trislab.Modules.Webhooks;
using Trislab.Modules.Billing;
using Trislab.Modules.Catalog;
using Trislab.Modules.Tickets;
using Trislab.Modules.Multitenancy.Features.v1.GetTenantStatus;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// The platform's settings (DATABASE_URL, REDIS_URL, …) become the options the building blocks read.
builder.Configuration.AddPlatformSettings();

// Serialize enums as string names (reads still accept names or integers). [Flags] enums (AuditTag, BodyCapture)
// opt back to numeric via their own NumericEnumConverter since comma-joined flag strings break bitwise consumers. Frontends mirror this as string unions.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

if (builder.Environment.IsProduction())
{
    static void Require(IConfiguration config, string key, string setting)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
        {
            throw new InvalidOperationException($"{setting} is not set. The stamp sets it; locally, .env sets it (docs/configuration.md).");
        }
    }

    // Postgres and the JWT signing key are required. Every other service falls back to what needs
    // no service: the in-memory cache without REDIS_URL, local disk without S3_ENDPOINT.
    var config = builder.Configuration;
    Require(config, "DatabaseOptions:ConnectionString", "DATABASE_URL");
    Require(config, "JwtOptions:SigningKey", "JWT_SIGNING_KEY");

    // Password-reset and confirmation links are built from FrontendOptions:DefaultOrigin when the request
    // has no allowed Origin. The stamps don't set it yet (docs/upstream.md row 57), so it is optional here,
    // and those flows answer 500 until it is set. When it is set, it must be an absolute http(s) URL:
    // on Unix, Uri.TryCreate also accepts "/path" as a file:// URI.
    var defaultOrigin = config["FrontendOptions:DefaultOrigin"];
    if (!string.IsNullOrWhiteSpace(defaultOrigin)
        && (!Uri.TryCreate(defaultOrigin, UriKind.Absolute, out Uri? origin)
            || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps)))
    {
        throw new InvalidOperationException(
            "FrontendOptions:DefaultOrigin must be an absolute http(s) URL, such as \"https://app.example.com\".");
    }
}

builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies = [
        typeof(GenerateTokenCommand),
        typeof(GenerateTokenCommandHandler),
        typeof(GetTenantStatusQuery),
        typeof(GetTenantStatusQueryHandler),
        typeof(Trislab.Modules.Auditing.Contracts.AuditEnvelope),
        typeof(Trislab.Modules.Auditing.Persistence.AuditDbContext),
        typeof(Trislab.Modules.Webhooks.Contracts.v1.CreateWebhookSubscription.CreateWebhookSubscriptionCommand),
        typeof(Trislab.Modules.Webhooks.WebhooksModule),
        typeof(Trislab.Modules.Billing.Contracts.BillingContractsMarker),
        typeof(Trislab.Modules.Billing.BillingModule),
        typeof(Trislab.Modules.Catalog.Contracts.CatalogContractsMarker),
        typeof(Trislab.Modules.Catalog.CatalogModule),
        typeof(Trislab.Modules.Tickets.Contracts.TicketsContractsMarker),
        typeof(Trislab.Modules.Tickets.TicketsModule),
        typeof(Trislab.Modules.Files.Contracts.v1.Commands.RequestUploadUrlCommand),
        typeof(Trislab.Modules.Files.FilesModule),
        typeof(Trislab.Modules.Chat.Contracts.v1.Commands.CreateChannelCommand),
        typeof(Trislab.Modules.Chat.ChatModule),
        typeof(Trislab.Modules.Notifications.Contracts.v1.Commands.MarkNotificationReadCommand),
        typeof(Trislab.Modules.Notifications.NotificationsModule)];
});

var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly,
    typeof(Trislab.Modules.Files.FilesModule).Assembly,
    typeof(WebhooksModule).Assembly,
    typeof(BillingModule).Assembly,
    typeof(CatalogModule).Assembly,
    typeof(TicketsModule).Assembly,
    typeof(Trislab.Modules.Chat.ChatModule).Assembly,
    typeof(Trislab.Modules.Notifications.NotificationsModule).Assembly,
};

// Tenancy first (docs/configuration.md, TenancyOptions): the mode is multi-tenant unless TenancyOptions:Mode says
// otherwise. A single-tenant run leaves out the multi-tenant-only modules (Multitenancy, Billing).
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

// The transactional outbox is framework infrastructure with exactly one owner
// (EventingDbContext), so the host registers it once for every module (issue #1349).
builder.Services.AddEventingCore(builder.Configuration);

builder.AddModules(moduleAssemblies);

// Self-heal deployments carrying retired per-module `{module}-outbox-dispatcher` Hangfire recurring jobs
// (the outbox is now dispatched by OutboxDispatcherHostedService). No-op once the storage is clean.
builder.Services.AddHostedService<App.Api.OrphanedOutboxRecurringJobCleanupService>();

// Demo data is provisioned by the DbMigrator's `seed-demo` verb, not the API — the API never mutates data on startup.
// See src/Host/App.DbMigrator/README.md.

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

// The platform's probes: /healthz while the process runs, /readyz once every health check passes:
// the module databases, Hangfire, and the cache when REDIS_URL is set.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/readyz").AllowAnonymous();

// Called through a front end, shows that its /api proxy reaches the API.
app.MapGet("/api/health/info", (IConfiguration config) => Results.Ok(new
   {
       name = "App.Api",
       version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
       environment = config["Platform:Environment"],
   }))
   .WithTags("Health")
   .AllowAnonymous();

app.MapGet("/", () => Results.Ok(new { message = "hello world!" }))
   .WithTags("PlayGround")
   .AllowAnonymous();

await app.RunAsync();