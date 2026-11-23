# tl-framework

The framework that Trislab's multi-tenant SaaS products and webapps are built on, with the code the SaaS starter will hold until `tl-product-saas` exists. Read this first; the detail is in [`docs/`](docs/).

## What it is

A complete SaaS starting point on .NET 10: a modular monolith with multi-tenancy, identity and the building blocks a SaaS product needs, an operator console and a tenant app in React, built on the [fullstackhero .NET starter kit](https://github.com/fullstackhero/dotnet-starter-kit) and adapted to the platform. Out of the box, operators create tenants with plans in `admin`, and each tenant's users sign in to `dashboard`, where the sample modules give them a product catalog, support tickets, chat, files and notifications.

## When to use it

Use it for a product that serves many customers from one deployment, each a tenant with its own users, plans and data.

| Your product… | Choose |
| --- | --- |
| Shows content, with no backend of its own | `tl-template-website` |
| Lets people work with data for one organisation, with its own API and database | `tl-product-webapp` |
| Serves many customers from one deployment, each a tenant with its own users, plans and data | `tl-product-saas` (not created yet; its code is in this repository until then) |

## How to use it

You don't create a product repository yourself. The platform creates it when your product is onboarded: the onboarding PR in `tl-cloud-infrastructure` adds the product to `catalog/products.yaml`, with its repository named `<customer>-saas-<product>` (for example `trislab-saas-construction`), and the pipeline creates the repository from the SaaS starter `tl-product-saas` with its team, rules and `dev` Environment. Products reference this repository, `tl-framework`, as packages from `v0.1.0`; until the starter exists, no SaaS product is onboarded. Don't use GitHub's "Use this template" button.

The same PR writes the first stamp. It declares the secrets `jwt-signing-key`, `hangfire-password` and `seed-admin-password`, and the backing services `postgres` and `storage`, and gives `api` internal ingress. It has no cache yet, so the API runs on its in-memory cache until the stamp adds `cache`.

The new repository gets a copy of the starter's files. Framework releases reach it as package upgrade PRs, and starter changes as `update-from-template` PRs; its own code, README and `docs/` are your team's.

## Features included

What works on the first deploy, with no change:

- **Multi-tenancy:** a tenant catalog, provisioning, plans and per-tenant themes; every tenant's data is filtered by tenant in every query.
- **Identity:** sign-in with JWTs, users, roles, permissions, groups, sessions, impersonation and two-factor sign-in.
- **The modules:** Auditing, Files, Billing, Notifications and Webhooks, and the samples Catalog, Tickets and Chat, to keep or delete ([modules.md](docs/modules.md)). `Architecture.Tests` guards their boundaries.
- **The building blocks:** persistence, caching, eventing with an outbox, background jobs with Hangfire, mail, file storage, quotas, rate limiting, security headers and OpenTelemetry.
- **The migrator:** before each revision of the API starts, it takes a Postgres lock, applies every module's migrations and the seed data, and the demo data where `SEED_DEMO` is true, and exits.
- `/api` and `/storage` on the front ends' own hostnames: nginx passes them to the API and to the storage, so neither needs a hostname or CORS settings.
- `/healthz` and `/readyz` on all three apps. The API is ready only while its database answers.
- `/config.json`, written when a front end starts from the `PLATFORM_*` and `PUBLIC_*` settings, so one image runs in every Environment.
- The check `ci / ci` on every PR and every push to `main`: it builds the four images, runs the unit, architecture and integration tests, scans the images with Trivy, and checks `platform.yaml`.
- Images that run as a non-root user, with every base image pinned by digest.

## Technologies

| Technology | Version |
| --- | --- |
| fullstackhero .NET starter kit | Release `10.0.0`, MIT licence ([`LICENSE`](LICENSE), which stays in every repository); every later commit on its `main` is reviewed and synced ([upstream.md](docs/upstream.md)) |
| .NET | SDK 10.0.404 (`global.json`), runtime 10.0.15 (`aspnet:10.0.15-noble-chiseled`) |
| ASP.NET Core | 10.0.15 |
| EF Core | 10.0.15, with Npgsql's provider 10.0.3 |
| Aspire (AppHost) | 13.6.5 |
| Hangfire | 1.8.23, stored in Postgres |
| Redis-compatible cache | Optional: the stamp's `cache`; Valkey 9.1.0 in the AppHost; in-memory without it |
| S3-compatible storage | The stamp's `storage`; rustfs 1.0.3 in the AppHost, `docker-compose.yml` and the integration tests; AWS SDK for S3 4.0.104.8 |
| RabbitMQ | Optional: the stamp's `messaging`; RabbitMQ.Client 7.2.1; in-memory without it |
| React | 19.3.1, with React Router 7 and TanStack Query 5 |
| Vite | 8.3.8 |
| TypeScript | 6.0.3 |
| Node.js | 22.23.4 (`node:22.23.4-alpine`, build stage of `admin` and `dashboard` only), with npm 10.9.9 |
| nginx | 1.31.7, mainline (`nginxinc/nginx-unprivileged:1.31.7-alpine`) |
| Postgres | 17.11 (`postgres:17.11-alpine`, in `docker-compose.yml`, the AppHost and the integration tests) |
| xUnit and Testcontainers | xUnit v3 4.0.2 (`xunit.v3.mtp-off`); Testcontainers 4.16.0 |
| Trivy | 0.74.0, in `ci / ci` |

Every base image is pinned by digest. Renovate will keep the versions current; until it runs, the platform team bumps them.

## Repository layout

| Path | Holds |
| --- | --- |
| `src/BuildingBlocks/` | The shared framework every module uses: persistence, caching, eventing, jobs, mail, storage, quotas, web |
| `src/Modules/` | One folder per module, each with a runtime project, its migrations, and a `.Contracts` project ([modules.md](docs/modules.md)) |
| `src/Samples/` | The samples Catalog, Tickets and Chat, with their tests: patterns to copy, never packed, and moving to `tl-product-saas` |
| `src/Host/` | `App.Api` with `Platform/PlatformConfiguration.cs`; `App.DbMigrator`, the init container of `api`; `App.AppHost` for local runs |
| `src/Tests/` | `Architecture.Tests`, a test project per module and building block, and `Integration.Tests` |
| `clients/admin/` | The operator console: React, its Dockerfile and `docker/` with nginx and `/config.json` |
| `clients/dashboard/` | The tenant app, laid out like `admin` |
| `docker-compose.yml`, `.env.example` | The four images locally, without the .NET SDK |
| `platform.yaml` | The images and the sizing of each app, within the stamp's caps |
| `.github/` | The `ci` workflow, the PR template and the code owners |
| `AGENTS.md`, `CLAUDE.md`, `.agents/skills/`, `.claude/` (rules, skill pointers, subagents, settings) | How coding agents work in this codebase |
| `docs/` | The detail behind this README |

## First steps

1. **Run it locally:** `dotnet run --project src/Host/App.AppHost`, after `npm install` in both clients; or, without the .NET SDK, `cp .env.example .env` and `docker compose up --build`, then open <http://localhost:8082>. Before you push, run `dotnet test src/App.slnx`. [getting-started.md](docs/getting-started.md) has the accounts to sign in with.
2. **Open your first PR:** change something in `src/` or `clients/`, push a branch and open a PR. `ci / ci` must pass and a code owner must approve.
3. **Deploy, release and promote:** a merge to `main` deploys to `dev`, a tag `vX.Y.Z` releases to the `release` tier, and a promotion moves a release to production. `deploy.yml` and `release.yml` do the first two, and the platform adds `promote.yml` before the first production Environment; [deployment.md](docs/deployment.md) explains how they work, and [its section 8](docs/deployment.md#8-steps-for-developers) lists your steps. The `release` skill runs them.
4. **Add a module:** a runtime project and a `.Contracts` project in `src/Modules/`, registered in both `Program.cs` files ([modules.md](docs/modules.md#2-add-a-module)). Copy from a sample in `src/Samples/`.
5. **Add a migration:** change the module's model, then `dotnet ef migrations add <Name>` into the module's own `Migrations/` folder. Keep it backward compatible ([deployment.md](docs/deployment.md#3-migrations)).
6. **Add a setting or a secret:** add it to the stamp, then map it in `src/Host/App.Api/Platform/PlatformConfiguration.cs` ([configuration.md](docs/configuration.md#5-add-a-setting-or-a-secret)).
7. **Choose a backing service:** add `cache` or `messaging` to a stamp, or move its Postgres to Neon, in its `stamp.yaml`; the app needs no change ([deployment.md](docs/deployment.md#6-choosing-a-backing-service)).

## More

- [Getting started](docs/getting-started.md): run the product with the AppHost or Docker Compose, the services, the accounts, and the tests.
- [Architecture](docs/architecture.md): the modular monolith, the images, the apps, the migrator, the backing services and the request path.
- [Configuration](docs/configuration.md): the settings and secrets, `PlatformConfiguration.cs`, `/config.json`, the `/api` and `/storage` upstreams, and how to add a setting or a secret.
- [Deployment](docs/deployment.md): Environments, deploy, release and promote, migrations, `platform.yaml` and the caps, choosing a backing service, and how to ask for more. Section 8: the steps for developers.
- [Modules](docs/modules.md): each module, how to add or delete one, and what `Architecture.Tests` enforces.
- [Packages](docs/packages.md): what is packed, the package names, the one framework version, and how a product references the packages and upgrades them.
- [Upstream](docs/upstream.md): how the platform team syncs each fullstackhero commit, and the status of every commit since `10.0.0`.
