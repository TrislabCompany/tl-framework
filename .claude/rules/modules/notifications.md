---
paths:
  - "src/Modules/Notifications/**"
  - "src/Tests/Notifications.Tests/**"
---

# Module: Notifications

Per-user in-app inbox (bell icon) driven by cross-module integration events, with live SignalR push. Supports both tenancy modes (`TenancyModes.All`) and references no multi-tenant-only module's runtime or `Multitenancy.Contracts`. The tenant-expiry e-mails are sent by the Multitenancy module, not here; the invoice e-mail (`InvoiceIssuedEmailHandler`) stays, and is inert without SubscriptionsUsage. Module `Order = 750` (**before Chat 800** so its handlers are registered before Chat publishes).

**Entities / DbContext:** `Notification` (aggregate: `UserId`, `Type`, `Title`/`Body`/`Link`, `Source`, `MetadataJson`, `ReadAtUtc`). `NotificationsDbContext`. Any module asks for a notification by publishing `UserNotificationRequestedIntegrationEvent` (in `Modules.Notifications.Contracts`, Events) with its own type, title, body, link and metadata; Chat does for a mention. Notifications references no publisher, so it never depends on a sample.
**Areas:** List, GetUnreadCount, MarkRead, MarkAllRead. Full list: `Features/v1/` or `/scalar`.

## Gotchas

- **It's a consumer.** New notification types come from **handling another module's integration event** (`AddIntegrationEventHandlers`), not from new endpoints. The handler writes an inbox row **and** pushes `"NotificationCreated"` to SignalR group `user:{userId}` via `IHubContext<AppHub>`.
- **Order matters** — Notifications (750) must load before any publisher whose events it consumes (Chat 800). If a new module publishes events Notifications should react to, mind the `Order`.
- In-memory bus runs handlers **synchronously in the publisher's request scope** — keep the handler minimal; an exception surfaces to the originating request. See `eventing.md`.
- Inbox rows are **denormalized** (Title/Body/Link/MetadataJson copied in) so rendering never calls back into the source module. `MarkRead` is idempotent (`ReadAtUtc ??= now`).
