---
paths:
  - "src/Modules/Multitenancy/**"
  - "src/Tests/Multitenancy.Tests/**"
---

# Module: Multitenancy

Tenant catalog, provisioning, activation/upgrade, per-tenant theming, and the tenant-expiry e-mails. **Multi-tenant mode only** (`SupportedTenancyModes = Multitenant`): a single-tenant host does not register it.

**Tenant resolution is not here.** The `Trislab.Framework.Tenancy` building block (`src/BuildingBlocks/Tenancy/`) owns it in both modes: `AddTrislabTenancy()` sets up Finbuckle (claim, header and `?tenant=` strategies and the distributed cache store in multi-tenant mode; the static strategy and an in-memory store in single-tenant mode), the event, outbox-drain and job tenant scopes, and `ITenantInitialPasswordBuffer`. This module adds only the catalog store behind the resolver (`EFCoreStore<TenantDbContext, AppTenantInfo>`), sign-up, lifecycle and the operator endpoints and middleware. Do not add tenant resolution here.

**Entities / DbContext:** `AppTenantInfo` (catalog), `TenantProvisioning` + `TenantProvisioningStep`, `TenantTheme`. `TenantDbContext` holds the tenant catalog in the main DB.
**Areas:** CreateTenant, ChangeTenantActivation, UpgradeTenant, Get(Tenants/Status/Migrations), TenantProvisioning (status/retry), TenantTheme (get/update/reset). Full list: `Features/v1/` or `/scalar`.

## Gotchas

- **Finbuckle pipeline ordering** — strategy chain Claim → Header → `?tenant=` (registered by the Tenancy block) → DistributedCache → EFCoreStore (this module's store, registered after the cache). `UseMultiTenant()` runs **before `UseAuthentication()`**, but Finbuckle's `ClaimStrategy` runs the default auth scheme's handler itself, so an authenticated caller resolves to its **tenant claim** and a `tenant` header cannot override it. The header (`MultitenancyConstants.Identifier`) decides only for anonymous requests (login, refresh); the root-operator override is post-auth middleware in `MultitenancyModule.ConfigureMiddleware`.
- **Tenant-expiry e-mails** (`IntegrationEventHandlers/`: nearing expiry, entered grace, expired) are sent from this module through the Mailing building block, because this module owns the events. Notifications has no reference to Multitenancy.
- **Root-operator cross-tenant override is a post-auth middleware** in `ConfigureMiddleware` (not a Finbuckle strategy). Gate: caller's JWT tenant claim == `MultitenancyConstants.Root.Id` **and** a `tenant` header != root; it re-resolves through the tenant store and sets the target with `ITenantAccessor.Use` for the rest of the request. **It only reads (D-44):** any other method than GET, HEAD and OPTIONS through the override throws 403 `Multitenancy.RootReadOnly` (`ReadOnlyRequest`; the one allowed POST is the endpoint named `SseToken`), never falling back to root. A new endpoint an operator must write to runs in root (`/tenants/{id}/…`), never through the header. Claim-aware tenant logic must go here, never in a strategy.
- **`ITenantInitialPasswordBuffer`** (singleton) — the tenant admin password is **operator-supplied**, not a constant. `CreateTenantCommandHandler` calls `Store(tenantId, password)` **before** kicking off provisioning; the background seed step `TryConsume`s it (`ConcurrentDictionary`, consume = remove).
- **Provisioning** runs 4 steps (Database → Migrations → Seeding → CacheWarm) via a Hangfire `TenantProvisioningJob`, falling back to inline execution if Hangfire storage is unavailable. **Activation is gated on `Status == Completed`.**
- `ITenantService.MigrateTenantAsync`/`SeedTenantAsync` create a fresh scope and set `IMultiTenantContext` **first**, then run the `IDbInitializer`s.

**Users are not tenant data.** Identity keeps global user accounts with a membership per tenant (`modules/identity.md`), always in the main database, even for a tenant with its own connection string. A tenant's admin e-mail that already has an account joins the new tenant with its own password; an operator's e-mail can't be a tenant's admin.

Tenant **isolation** mechanics (default-on filter, `IGlobalEntity` opt-out, `base.OnModelCreating` last) live in `database.md`.
