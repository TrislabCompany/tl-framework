# tl-framework

The framework that Trislab's multi-tenant SaaS products and webapps are built on. It holds only packages and tests: the building blocks and modules a product references as NuGet and npm packages, and the tests that guard them. The apps built on them (the host, the migrator, the React apps and the sample modules) live in the starters, `tl-product-saas` and `tl-product-webapp`. Read this first; the detail is in [`docs/`](docs/).

## What it is

The packages of a modular .NET 11 monolith: multi-tenancy, identity and the building blocks a SaaS product needs, and the React package `framework-ui`. It is derived from an MIT-licensed open-source starter kit ([THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)) and adapted to the platform. Nothing here runs by itself except the test host, `src/Tests/TestHost`, which the integration, performance and security tests use.

## When to use it

Use it for a product that serves many customers from one deployment, each a tenant with its own users, plans and data.

| Your product… | Choose |
| --- | --- |
| Shows content, with no backend of its own | `tl-template-website` |
| Lets people work with data for one organisation, with its own API and database | `tl-product-webapp` |
| Serves many customers from one deployment, each a tenant with its own users, plans and data | `tl-product-saas` |

## How to use it

You don't use this repository directly. A product is created from a starter by the platform's onboarding pull request in `tl-cloud-infrastructure` (its repository is named `<customer>-saas-<product>`), and the starter references the packages of this repository from `v0.1.0`. Framework releases reach the product as package upgrade PRs; see [packages.md](docs/packages.md). Don't use GitHub's "Use this template" button.

## Features included

What the packages give a product, with no change:

- **Multi-tenancy:** a tenant catalog, provisioning, plans and per-tenant themes; every tenant's data is filtered by tenant in every query.
- **Single-tenant mode:** the same framework for an app that serves one organisation: set `TenancyOptions:Mode` to `SingleTenant` and name the fixed tenant. There is no tenant catalog, sign-up, plan or operator console, a sign-in names no tenant, and the data is still stamped with the tenant. Every module declares the modes it supports ([modules.md](docs/modules.md#5-tenancy-modes), [architecture.md](docs/architecture.md#2-tenancy-mode)).
- **Identity:** sign-in with JWTs, global users with a membership per tenant (one account, a tenant chooser and a tenant switch), roles, permissions, groups, sessions, impersonation and two-factor sign-in.
- **The modules:** Auditing, Files, SubscriptionsUsage, InvoiceDocuments, Notifications and Webhooks ([modules.md](docs/modules.md)). `Architecture.Tests` guards their boundaries. The sample modules Catalog, Tickets and Chat are in `tl-product-saas`.
- **The building blocks:** persistence, caching, eventing with an outbox, background jobs with Hangfire, mail, file storage, quotas, rate limiting, security headers and OpenTelemetry.
- **Migrations:** each module ships its own, in its package; the product's migrator applies them (`tl-product-saas`).
- **The check `ci / ci`:** on every PR and every push to `main`, it runs the unit, architecture and integration tests and the tests of each npm package.

## Technologies

| Technology | Version |
| --- | --- |
| Upstream starter kit | Release `10.0.0`, MIT licence ([THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), which ships with every package); every later commit on its `main` is reviewed and synced ([upstream.md](docs/upstream.md)) |
| .NET | SDK 11.0.100 (`global.json`); the test host image on `sdk:11.0.100-resolute` and `aspnet:11.0.0-resolute-chiseled` (Ubuntu 26.04) |
| ASP.NET Core | 11.0.0 |
| EF Core | 11.0.0, with Npgsql's provider 11.0.0 |
| Finbuckle.MultiTenant | 11.0.0, under its Open Source Maintenance Fee EULA, accepted with `AcceptFinbuckleEULA` in `src/Directory.Build.props` (tl-product-ideas D-41); modules read and set the tenant through `ITenantAccessor` only |
| Hangfire | 1.8.23, stored in Postgres |
| Redis-compatible cache | Optional; in-memory without it |
| S3-compatible storage | rustfs 1.0.3 in the integration tests; AWS SDK for S3 4.0.104.8 |
| RabbitMQ | Optional; RabbitMQ.Client 7.2.1; in-memory without it |
| React, TypeScript | React 19.3.1, TypeScript 6.0.3, in `packages/framework-ui`; Node.js 24 with npm |
| Postgres | 17.11 (`postgres:17.11-alpine`, in the integration tests) |
| xUnit and Testcontainers | xUnit v3 4.0.2 (`xunit.v3.mtp-off`); Testcontainers 4.16.0 |

Renovate keeps the versions current.

## Repository layout

| Path | Holds |
| --- | --- |
| `src/BuildingBlocks/` | The shared framework every module uses: persistence, caching, eventing, jobs, mail, storage, quotas, tenancy, web |
| `src/Modules/` | One folder per module, each with a runtime project, its migrations, and a `.Contracts` project ([modules.md](docs/modules.md)) |
| `packages/` | The npm packages, starting with `framework-ui` ([packages.md](docs/packages.md)) |
| `src/Tests/` | `Architecture.Tests`, a test project per module and building block, `Integration.Tests`, and `TestHost`, the test-only host |
| `.github/` | The `ci` and `packages` workflows, the PR template and the code owners |
| `AGENTS.md`, `CLAUDE.md`, `.agents/skills/`, `.claude/` (rules, skill pointers, subagents, settings) | How coding agents work in this codebase |
| `docs/` | The detail behind this README |

## First steps

1. **Build and test:** `dotnet test src/App.slnx` (Docker must run) and `npm test` in `packages/framework-ui`. [getting-started.md](docs/getting-started.md) has the details.
2. **Open your first PR:** change something in `src/` or `packages/`, push a branch and open a PR. `ci / ci` must pass and a code owner must approve.
3. **Release:** a tag `vX.Y.Z` on `main`, created by an organization owner, publishes every package ([packages.md](docs/packages.md#2-one-version-for-everything)).
4. **Add a module:** a runtime project and a `.Contracts` project in `src/Modules/`, registered in the test host ([modules.md](docs/modules.md#2-add-a-module)). The product registers it in its own host.
5. **Add a migration:** change the module's model, then `dotnet ef migrations add <Name>` into the module's own `Migrations/` folder. Keep it backward compatible once products are in production; in development mode it may break (`AGENTS.md`).

## More

- [Getting started](docs/getting-started.md): what you need, the tests, and the test host.
- [Architecture](docs/architecture.md): the modular monolith and the tenancy modes.
- [Modules](docs/modules.md): each module, how to add or delete one, and what `Architecture.Tests` enforces.
- [Bot protection](docs/bot-protection.md): Cloudflare Turnstile on public forms, `RequireTurnstile()` and the `Turnstile` widget, and their keys.
- [Packages](docs/packages.md): what is packed, the package names, the one framework version, and how a product references the packages and upgrades them.
- [Upstream](docs/upstream.md): how the platform team syncs each upstream commit, and the status of every commit since `10.0.0`.

## Licence

Proprietary: [LICENSE](LICENSE). The code is public to read, but it belongs to Trislab d.o.o., and only Trislab, and the clients of the applications Trislab delivers under a written agreement, may use it. Cloning or forking it for use, copying, redistribution and training AI models on it are not allowed. The open-source code it is derived from keeps its own licence: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), which every package ships with.
