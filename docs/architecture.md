# Architecture

How the parts of the framework fit together. The framework holds only packages and tests; the host, the migrator, the images and the React apps that run them are in the starters (`tl-product-saas`, `tl-product-webapp`).

## 1. A modular monolith

The API is one .NET 10 process made of modules. Each module is a bounded context with its own entities, its own `DbContext` and its own endpoints under `/api/v1/<module>`, built as vertical slices: one folder per feature, with its command or query, handler, validator and endpoint. Modules talk to each other only through each other's `.Contracts` project, or through integration events with an outbox in Postgres. The building blocks in `src/BuildingBlocks` give every module the same persistence, caching, eventing, jobs, mail, storage, quotas and web conventions. [modules.md](modules.md) lists the modules.

Every tenant's data is in the same database, kept apart by a tenant column that EF Core filters on in every query. The host runs in one of two tenancy modes, set by `TenancyOptions:Mode` (section 2):

- **Multi-tenant** (the default, a SaaS product): many tenants from a tenant catalog. A request names its tenant in the `tenant` header, and a user signs in within one tenant.
- **Single-tenant** (a webapp): one fixed tenant, set in configuration. There is no tenant catalog, sign-up, plan, quota or operator console, and a request names no tenant: the server always resolves the fixed tenant, and a token still carries its tenant claim. The data is still stamped with the tenant, so a module's code is the same in both modes.

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

The `SingleTenant` values are required, and checked at start-up, only in single-tenant mode. In that mode the API and the migrator register no tenant catalog and leave out the multi-tenant-only modules (Multitenancy, SubscriptionsUsage); a module that does not support the mode, listed by hand, stops the start-up with a message that names it. The quota service is the no-op one, and the quota middleware is not added. Sign-in, refresh and the password endpoints take no `tenant` header. Set the same values for `api` and `migrator`.

## 3. Migrations

Each module ships its own migrations, in the `Migrations/` folder of its runtime project, and keeps its migrations history in its own schema, so a product that leaves a module out gets none of its tables. Each module started from one `Baseline` migration. The previous revision runs on the migrated database until the new one is ready, so every migration must be backward compatible (a product's migrator applies them, as the test host does in its provisioning pass).
