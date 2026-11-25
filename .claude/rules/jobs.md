---
paths:
  - "src/BuildingBlocks/Jobs/**"
  - "src/**/*Job*.cs"
---

# Background jobs (Hangfire)

`src/BuildingBlocks/Jobs/`. Read before enqueuing or scheduling work.

## Fire-and-forget / scheduled — `IJobService`

Inject `IJobService` (`Jobs/Services/IJobService.cs`) and use it. Never Hangfire's statics (`BackgroundJob`, `RecurringJob`, `JobStorage.Current`, `GlobalConfiguration`): they belong to whichever host configured Hangfire last. The storage, the activator (`TrislabJobActivator`) and the filters are per-host DI singletons; inject `JobStorage`, `IBackgroundJobClient` or `IRecurringJobManager` when you need them. `JobsDependencyInjectionTests` (Architecture.Tests) fails on a static.

```csharp
jobService.Enqueue<PurgeJob>(j => j.RunAsync(tenantId, CancellationToken.None));    // default queue
jobService.Enqueue<PurgeJob>("email", j => j.RunAsync(tenantId, CancellationToken.None));
jobService.Schedule(() => DoLater(), TimeSpan.FromMinutes(5));
```

Queues: `default`, `email` (5 workers, 30s poll). Storage auto-selected from `DatabaseOptions.Provider` (Postgres/MSSQL).

**A job's arguments are stored in clear** in the job tables, and shown in the dashboard, until the job expires. Pass ids, never secrets, tokens or message bodies.

## E-mail in the background — `IBackgroundMailer`

Queue e-mail with `IBackgroundMailer.Enqueue(request)` (`Jobs/Mail/`), never `jobService.Enqueue(() => mailService.SendAsync(request, …))`. The queue encrypts the message with ASP.NET Data Protection (purpose `Trislab.Framework.Mailing.Queue.v1`) and runs `SendQueuedMailJob` on the `email` queue, so invitation, reset and confirmation links never sit readable in the job store. The key ring is the host's: Redis when caching is configured, which every replica shares. `QueuedMailTests` (Architecture.Tests) fails on a send inside a job expression.

## Recurring jobs — `IRecurringJobManager`

`IJobService` has **no** recurring API. Register recurring jobs in the module's `MapEndpoints` with `IRecurringJobManager.AddOrUpdate<T>(...)`, always `TimeZoneInfo.Utc`:

```csharp
recurringJobs.AddOrUpdate<PurgeOrphanedFilesJob>("files:purge-orphaned",
    j => j.RunAsync(CancellationToken.None), Cron.Hourly(), new() { TimeZone = TimeZoneInfo.Utc });
```

Examples in the tree: `PurgeOrphanedFiles`/`PurgeDeletedFiles` (Files), `MonthlyInvoiceJob` (SubscriptionsUsage), `AuditRetentionJob` (Auditing), `WebhookDispatchJob` (Webhooks).

## Dashboard & config

`/jobs` (default), behind `HangfireOptions.UserName`/`Password` basic auth — both `[Required]`, password `[MinLength(12)]`, so **startup fails in non-dev if unset**.

## Gotchas

- Jobs run on the server with **no HTTP/tenant context** — set the tenant inside the job (`using var tenantUse = tenants.Use(tenant);`, `ITenantAccessor`), then create a fresh scope and resolve the tenant-filtered DbContext from it; `Use` restores the previous tenant when disposed, so a job can walk every tenant in turn. A job enqueued from a request carries that request's tenant; in single-tenant mode a job with no tenant parameter (a recurring job) runs in the fixed tenant (`TrislabJobActivator` falls back to `ISingleTenantProvider`).
- A product's migrator registers `NoOpJobService` whose methods **throw** — surfaces any accidental enqueue during migration. Don't enqueue from migration/seed paths.
- A job class is a normal DI-resolved type (scope-per-job via `TrislabJobActivator`); inject what you need.
