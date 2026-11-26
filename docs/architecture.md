# Architecture

How the parts of the framework fit together. The framework holds only packages and tests; the host, the migrator, the images and the React apps that run them are in the starters (`tl-product-saas`, `tl-product-webapp`).

## 1. A modular monolith

The API is one .NET 11 process made of modules. Each module is a bounded context with its own entities, its own `DbContext` and its own endpoints under `/api/v1/<module>`, built as vertical slices: one folder per feature, with its command or query, handler, validator and endpoint. Modules talk to each other only through each other's `.Contracts` project, or through integration events with an outbox in Postgres. The building blocks in `src/BuildingBlocks` give every module the same persistence, caching, eventing, jobs, mail, storage, quotas and web conventions. [modules.md](modules.md) lists the modules.

Every tenant's data is in the same database, kept apart by a tenant column that EF Core filters on in every query. The host runs in one of two tenancy modes, set by `TenancyOptions:Mode` (section 2):

- **Multi-tenant** (the default, a SaaS product): many tenants from a tenant catalog. A signed-in request's tenant is the token's `tenant` claim; an anonymous request names it in the `tenant` header.
- **Single-tenant** (a webapp): one fixed tenant, set in configuration. There is no tenant catalog, sign-up, plan, quota or operator console, and a request names no tenant: the server always resolves the fixed tenant, and a token still carries its tenant claim. The data is still stamped with the tenant, so a module's code is the same in both modes.

**Users are global** (section 2a): one account per e-mail address, with a membership in each tenant it belongs to. Everything a tenant grants (roles, groups, sessions) stays in that tenant.

The `Tenancy` building block (`Trislab.Framework.Tenancy`) resolves the tenant and owns the event, outbox and job tenant scopes in both modes: `AddTrislabTenancy()` in `Program.cs` before the platform and the modules, and `UseTrislabTenancy()` before `UseTrislabPlatform`. The Multitenancy module adds only what a multi-tenant product needs on top: the tenant catalog behind the resolver, sign-up, the lifecycle and the operator endpoints. Every module declares the modes it supports (`IModule.SupportedTenancyModes`), and start-up refuses a module registered in a mode it doesn't support ([modules.md](modules.md#5-tenancy-modes)).

## 2. Tenancy mode

The tenancy mode is not a platform setting: it comes from the starter the app was copied from, and is read from the options `TenancyOptions` (appsettings, or the environment variables `TenancyOptions__Mode` and so on). `Program.cs` reads it through `AddTrislabTenancy()`; no other code reads the environment.

| Option | Values | Meaning |
| --- | --- | --- |
| `TenancyOptions:Mode` | `Multitenant` (default), `SingleTenant` | `Multitenant`: many tenants from the tenant catalog (a SaaS product). `SingleTenant`: one fixed tenant (a webapp) |
| `TenancyOptions:SingleTenant:Id` | 1 to 64 lower-case letters, digits or hyphens; not `root` | The tenant id, stamped on every row and carried in the token's `tenant` claim |
| `TenancyOptions:SingleTenant:Name` | Text | The organisation's name |
| `TenancyOptions:SingleTenant:Country` | ISO 3166-1 alpha-2, such as `SI` | Selects the country rule pack |
| `TenancyOptions:SingleTenant:AdminEmail` | An e-mail address | The first administrator, seeded by the migrator with `SEED_ADMIN_PASSWORD` |

The `SingleTenant` values are required, and checked at start-up, only in single-tenant mode. In that mode the API and the migrator register no tenant catalog and leave out the multi-tenant-only modules (Multitenancy, SubscriptionsUsage); a module that does not support the mode, listed by hand, stops the start-up with a message that names it. The quota service is the no-op one, and the quota middleware is not added. Sign-in, refresh and the password endpoints need no `tenant` header in either mode (section 2a). Set the same values for `api` and `migrator`.

## 2a. Users, memberships and sign-in

A user account is global: one per e-mail address, with one password, one second factor and one profile. Access to a tenant is a **membership** (`TenantMembership`), and a user's roles, groups and sessions belong to the membership's tenant. The Identity module keeps all of this in the main database, never a tenant's own database.

- **Sign-in** takes e-mail and password only (`POST /api/v1/identity/token/issue`). It opens the tenant the optional `tenant` header names when the user is a member there, else the user's default tenant, else their only tenant. A user with several tenants and no default gets `tenantSelectionRequired`, the list and a `selectionToken` (5 minutes), and sends the choice to `POST /token/select`, optionally with `setDefault`.
- **The tenant menu** reads `GET /profile/tenants` and switches with `POST /token/switch`, which is audited. A refresh (`POST /token/refresh`) needs no tenant: the session that holds the refresh token names it.
- **Operators** are the members of the root tenant. They always sign in to root, never join another tenant, and act as a tenant's user only through impersonation. A root operator's request may name another tenant in the `tenant` header, and is then scoped to that tenant for that request, with the operator's own permissions from root (`MultitenancyModule`, `TenantHeaderOverrideTests`). The admin console uses it to look into a tenant: its users and their roles (`GET /users/search`, `GET /users/{id}/roles`), the users to impersonate, its branding. Anyone else's header is ignored: the token's claim wins.
- **Adding a user is an invitation** (`POST /invitations`). No administrator sets or learns a password, or confirms an e-mail. The membership stays pending, and the account invisible to the tenant, until its owner accepts the e-mailed link (`POST /invitations/accept`). A new address gets an account without a password, and its owner chooses one when accepting; so does an account whose e-mail nobody confirmed (someone may have signed up with another person's address), and what that sign-up joined is dropped. A confirmed account accepts while signed in, and its password is never touched. Accepting is one transaction, and a deactivated or lapsed tenant takes no new members. The token is 256 random bits, stored only as a hash, works once, expires after 7 days (`Identity:Invitations:LifetimeHours`) and can be revoked (`DELETE /invitations/{id}`). The answer to an invitation is the same whether the address has an account.
- **Self-registration** (`POST /self-register`, not in root) answers 202 `check_your_email`, with the same body and the same least duration (1 s, `Identity:SelfRegistration:MinimumResponseTime`) whether or not the address has an account; the owner of an existing account gets an e-mail instead. Forgot-password has the same floor, and a reset for an unknown address fails like a wrong token.
- **Every step after the password re-checks** the account (e-mail confirmed, not locked out, security stamp unchanged), the membership (active), the tenant (active, not expired) and the app boundary: the selection (its token works once), every refresh and every switch. A password change or reset ends every session of the account; so does a second-factor change at the next refresh. A refresh token is rotated atomically, and presenting an already rotated one ends the session (reuse detection).
- **Deactivating** a user in a tenant deactivates the membership and ends their sessions there; **removing** one (`DELETE /users/{id}`) deletes the membership and the user's roles, groups and sessions in that tenant. The account and its other tenants stay.
- **Audit:** invitations (sent, accepted, revoked, expired), memberships (added, removed, deactivated, activated), tenant switches, role grants and revocations, password changes and resets, second-factor changes, lockouts and refresh-token reuse are security events in the audit trail.

## 3. Migrations

Each module ships its own migrations, in the `Migrations/` folder of its runtime project, and keeps its migrations history in its own schema, so a product that leaves a module out gets none of its tables. Each module started from one `Baseline` migration. The previous revision runs on the migrated database until the new one is ready, so every migration must be backward compatible (a product's migrator applies them, as the test host does in its provisioning pass). Not yet: in development mode a migration may break, with no data migration (`AGENTS.md`, "Development mode").
