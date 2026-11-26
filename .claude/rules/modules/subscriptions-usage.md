---
paths:
  - "src/Modules/SubscriptionsUsage/**"
  - "src/Tests/SubscriptionsUsage.Tests/**"
---

# Module: SubscriptionsUsage (formerly Billing)

How a product charges its own tenants (the capability `subscriptions-usage`): plans, subscriptions, usage metering, monthly usage invoicing. Not the documents a tenant issues to its own customers: that is the InvoiceDocuments module. The module was renamed from Billing; the schema `billing`, the routes `/api/v1/billing`, the permission names `Permissions.Billing.*`, the `Billing` config section and the type names (`BillingDbContext`, `BillingPlan`, ...) are unchanged on purpose, so behaviour, data and migrations stay as they were. **Manual payment marking — no payment provider.** Module `Order = 500`.

**Permissions.** `Billing.View` is basic: a tenant reads its own subscription, invoices, usage and wallet, and requests a top-up. `Billing.Manage` is **root-only** (`IsRoot: true`): plans are global, and assigning subscriptions, issuing, voiding or marking invoices paid, approving top-ups and capturing usage are the operator's work. A tenant admin who held it could change every tenant's plans or credit its own wallet. Never add a tenant-level action under `Billing.Manage`; give it its own permission.

**Entities / DbContext:** `BillingPlan`, `Subscription`, `Invoice` (+ `InvoiceLineItem`), `UsageSnapshot`. **`BillingDbContext : DbContext`** (NOT `BaseDbContext`) — billing lives in the main DB with an explicit `TenantId` column for cross-tenant admin visibility, filtered in query services. Contracts = DTOs; `IBillingService`/`IUsageReporter` are internal.
**Areas:** Plans, Subscriptions, Invoices (generate/issue/mark-paid/void), Usage (capture/get), Limits (meters, plan limits, my usage). Monthly invoice job (`5 0 1 * *`). Full list: `Features/v1/` or `/scalar`.

## Gotchas

- **`BillingPlan` is `IGlobalEntity`** — platform-wide catalogue rows, **not tenant-scoped** (opts out of tenant isolation). A plan's `Key` matches the quota config key (e.g. `"pro"`): limits come from `QuotaOptions`, prices/overage from the plan.
- **`BillingDbContext` is a plain `DbContext`** — tenant filtering is done explicitly in query services, not by the `BaseDbContext` auto-filter. Don't assume the global tenant filter applies here.
- **Invoice state machine** — `Draft → Issued → Paid | Void`. Line items only addable in Draft; a Paid invoice can't be voided; totals recompute on add; Issue defaults due = +14 days.
- **Usage metering is idempotent** — `IUsageReporter.CaptureForPeriodAsync` reads `IQuotaService` and persists one `UsageSnapshot` per `QuotaResource` per (tenant, period), so invoicing math is reproducible even after a mid-period plan change.
- **Usage limits are the product's meters, not `QuotaResource`.** A product declares `IUsageMeter` (a counter) or `IUsageGauge` (what exists now) in `Contracts.Limits`; root sets a plan's limit per meter (`PlanLimits` jsonb on `BillingPlan`). `IUsageLimits` is the only way to check or count: `ConsumeAsync` for counters (one atomic upsert into `UsageCounters`, so concurrent calls never pass a hard limit, and a refused call counts nothing), `EnsureWithinLimitAsync` before adding to a gauge. A hard limit is a 409 `Billing.LimitReached`. Root is never limited. Don't add limits to `QuotaOptions` or `QuotaResource`: those stay the building block's API-call and storage quotas. Details: `docs/modules.md` section 8.
- **Counters' period is the calendar month (UTC)** until each tenant gets its own billing period (`UsageLimits.CurrentPeriodStart`).
