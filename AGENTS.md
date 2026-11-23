# This repository

> `tl-framework`: the framework every Trislab SaaS product and webapp is built on. A modular .NET 10
> monolith + two React 19 apps, built on the fullstackhero .NET starter kit and adapted to the platform
> contract (`README.md`, `docs/`).

## Role

The same block starts the `AGENTS.md` of every Trislab repository, so an agent in any of them knows where a change belongs.

- **For:** the framework of the Trislab SaaS products and webapps: the core building blocks and every reusable module, released as packages that products reference.
- **Contains:**
  - the core building blocks in `src/BuildingBlocks/` (`Trislab.Framework.*`);
  - every reusable module in `src/Modules/`, with its `.Contracts` (`Trislab.Modules.*`) and its own migrations (each will declare the tenancy modes it supports, with single-tenant mode);
  - country rule packs (`*-si`), when they come;
  - the migrator, and the React apps `clients/admin` and `clients/dashboard` (later the packages `framework-admin`, `framework-dashboard` and `framework-ui`);
  - the framework's agent skills, and `docs/upstream.md`, the log of the fullstackhero ports.
  - Until `tl-product-saas` exists, also the hosts and the samples Catalog, Tickets and Chat (`src/Samples/`), which move there.
- **Never contains:** a product's own features; Trislab-only code; customer names, secrets or prices; live infrastructure.
- **A change goes elsewhere when it is:**
  - starter scaffolding (the host, the AppHost, `init-product`, the template manifest): `tl-product-saas` (not created yet) or `tl-product-webapp`;
  - a need of one product only: its product repository. It moves here when a second product needs it;
  - a reusable infrastructure module, stamp template or app workflow: `tl-platform`;
  - a repository, environment or hostname: `tl-cloud-infrastructure`;
  - a product idea, plan, decision or hand-off plan: `tl-product-ideas` (private).
- **Released as:** one lockstep train `vX.Y.Z` of NuGet and npm packages on GitHub Packages, from `v0.1.0` (not published yet; `docs/packages.md`). Both starters deploy each release to the sandbox and smoke-test it, which is the gate.
- **Skills:** `.agents/skills/`, listed under [AI tooling resources](#ai-tooling-resources).
- **Scripts:** `scripts/`, listed with when to use each in [`scripts/README.md`](scripts/README.md); read it first. Each script prints its help with `pwsh <script> -?`.

## This guide

This file is the canonical guide for **all** AI coding tools (Claude Code, Cursor, Codex, …).
`CLAUDE.md` is a thin bridge that imports this file — edit conventions **here**, not there.

This file is the map. Detailed conventions live in `.claude/rules/`. Each rule has `paths:` front matter, so
Claude Code loads it by itself when you touch a matching file; other tools: **read the relevant rule file
before working in that area** (see the index below). Keep this file lean.

## What this is

A **modular monolith** (Vertical Slice Architecture) backend that ships with two **React + Vite**
front-ends. Multitenancy, auth, auditing, billing, files, chat and more are first-class.

- **Backend** — .NET 10, EF Core 10, PostgreSQL, Redis, JWT + ASP.NET Identity, Finbuckle multitenancy,
  Hangfire, OpenAPI/Scalar, Serilog + OpenTelemetry, .NET Aspire.
- **Frontends** — `clients/admin` (operator-facing) and `clients/dashboard` (tenant-facing): React 19,
  Vite 8, TypeScript 6, TanStack Query v5, React Router 7, Radix + Tailwind v4 (shadcn-style), SignalR/SSE.

## Repo map

| Path | What |
|------|------|
| `src/BuildingBlocks/` | Shared framework libraries (Core, Persistence, Web, Caching, Eventing, Storage, Quota…). **Protected — see below.** |
| `src/Modules/{Name}/` | Bounded contexts. Each has a runtime project + a `.Contracts` project (its only public API), and ships its own EF migrations in `Migrations/`. |
| `src/Samples/{Name}/` | The samples Catalog, Tickets and Chat, with their tests: patterns to copy, not part of the framework. Never packed; nothing in `src/BuildingBlocks` or `src/Modules` may reference them. They move to `tl-product-saas`. |
| `src/Host/App.Api` | Composition-root Web API host. `Platform/PlatformConfiguration.cs` maps the platform's settings onto options. |
| `src/Host/App.AppHost` | .NET Aspire orchestrator (Postgres, Valkey, rustfs storage, migrator, API, **both React apps**). |
| `src/Host/App.DbMigrator` | One-shot migrate/seed runner, the init container of `api`. DB is **not** migrated at API startup. |
| `src/Tests/` | Per-module tests, `Architecture.Tests` (NetArchTest), `Integration.Tests` (Testcontainers). |
| `clients/admin`, `clients/dashboard` | The two React apps, each with its Dockerfile and `docker/` (nginx, `/config.json`). |
| `docker-compose.yml`, `.env.example` | The four images locally, without the .NET SDK. |
| `platform.yaml`, `.github/workflows/ci.yml` | The images and app sizing for the platform; the `ci / ci` check. |
| `.github/workflows/packages.yml` | On a release tag, publishes the NuGet and npm packages to GitHub Packages (`docs/packages.md`). |
| `scripts/` | Scripts for repetitive work, listed in `scripts/README.md` (porting fullstackhero commits). |
| `docs/` | How the repository works on the platform: getting started, architecture, configuration, deployment, modules, packages. |
| `renovate/framework.json` | The Renovate preset products extend for framework upgrades (`docs/packages.md`). |

## Tech stack

| Backend | | Frontend | |
|---|---|---|---|
| Framework | .NET 10 / C# latest | Framework | React 19 + Vite 8 + TS 6.x |
| CQRS | Mediator 3.x (source-gen) | Data | TanStack Query v5 |
| Validation | FluentValidation 12.x | Routing | React Router 7 |
| ORM / DB | EF Core 10 / PostgreSQL (Npgsql) | UI | Radix + Tailwind v4 + CVA (shadcn) |
| Auth | JWT Bearer + ASP.NET Identity | Forms | react-hook-form + zod (**admin only**) |
| Multitenancy | Finbuckle 10.x | Realtime | `@microsoft/signalr`, SSE (dashboard) |
| Cache / Jobs | Redis, Hangfire | Tests | Playwright (route-mocked) |
| Docs | OpenAPI + Scalar | API client | hand-written `apiFetch` (no codegen) |
| Hosting | .NET Aspire locally, Azure Container Apps | Env | runtime `/config.json` (`PLATFORM_*`, `PUBLIC_*`) |
| Testing | xUnit v3, Shouldly, NSubstitute, AutoFixture, NetArchTest, Testcontainers | | |

## Build & run

```bash
# Whole stack (Postgres + pgAdmin + Valkey + rustfs + migrator + API + both React apps)
dotnet run --project src/Host/App.AppHost   # one-time: npm install in clients/admin & clients/dashboard

dotnet build src/App.slnx                   # build backend
dotnet run --project src/Host/App.Api       # API only → https://localhost:7030 (/scalar)
dotnet test src/App.slnx                    # tests — integration tests REQUIRE Docker

cd clients/admin && npm install && npm run dev       # → http://localhost:5173
cd clients/dashboard && npm install && npm run dev   # → http://localhost:5174
```

Migrations / seed (DbMigrator, separate step):
```bash
DATABASE_URL=... dotnet run --project src/Host/App.DbMigrator -- apply [--seed]
dotnet run --project src/Host/App.DbMigrator -- list-pending
```

**Ports:** API 7030 (https)/5030 (http) · admin 5173 · dashboard 5174 · Postgres 5432 · pgAdmin 5050 · Valkey 6379 · storage 9000. With `docker compose`: admin 8081 · dashboard 8082.

## Git and review

- Branch from `main` with a short kebab-case topic (`billing-proration`). One change per PR; a workflow change goes in its own PR.
- The PR title is an imperative plain sentence of 72 characters or fewer; it becomes the squash commit. The body follows `.github/pull_request_template.md`.
- A PR merges with the check `ci / ci` green and one code owner's approval, never your own. The author resolves the threads.
- Releases are tags `vX.Y.Z`, created only by an organization owner and never moved (`release` skill). `.github/workflows/ci.yml` only calls the platform's reusable workflow: never add steps to it.
- Before asking for review, run the `code-reviewer` and `architecture-guard` subagents. A review checks: correctness and edge cases, a test for each behaviour change, logs per `logging.md`, contracts compatible per `api-conventions.md`, docs updated, no secrets.

## Platform contract (do not break)

- Every image that serves traffic listens on **8080** and runs as a **non-root** user; base images are pinned by digest.
- The API answers `/healthz` and `/readyz`; the front ends' nginx answers both too.
- The app reads the platform's settings (`DATABASE_URL`, `REDIS_URL`, `S3_*`, `JWT_SIGNING_KEY`, …) **only** in `src/Host/App.Api/Platform/PlatformConfiguration.cs`; everything else reads options. See `docs/configuration.md`.
- The front ends call the API at the relative path `/api` and storage at `/storage`, through their nginx; `/config.json` holds only `PLATFORM_*` and `PUBLIC_*` keys.
- Migrations are backward compatible: the previous API revision keeps serving on the migrated database.

## Golden rules (do not break)

1. **Module boundaries** — a module references another module only through its `.Contracts` project, never its runtime project. Enforced by `Architecture.Tests`.
2. **Registering a module touches FOUR places** — `Program.cs` Mediator `o.Assemblies` (two markers each) + `moduleAssemblies` array, **and the identical pair in `DbMigrator/Program.cs`**. A missing Mediator marker = handlers silently undiscovered. See `architecture.md`.
3. **Tenant isolation is default-ON** via `BaseDbContext`. Opt out only via `IGlobalEntity`. Subclass DbContexts call `base.OnModelCreating` **last**. See `database.md`.
4. **Do NOT modify `src/BuildingBlocks`** without explicit approval — shared by every module, wide blast radius.
5. **Mediator handlers must be `public sealed`**, return `ValueTask<T>`, and `.ConfigureAwait(false)` every await.
6. **Structured logging only** — no string interpolation in log messages; use message templates / `[LoggerMessage]`.
7. **Propagate `CancellationToken`** into every EF/IO call; add as `= default` on public service methods.
8. **Every command handler + paginated query handler needs a validator** (`{Name}Validator`). Enforced by `Architecture.Tests`.
9. **Frontend: pass per-call data through `mutate(arg)`**, never via state the mutation callbacks close over (execute-time race). See `frontend/shared.md`.
10. **Docs travel with the change** — a change to a setting, a module, an endpoint the front ends use or the way the product runs isn't done until `README.md` and `docs/` match it.

## Rules index — read the relevant file before you work

**Backend / cross-cutting** (`.claude/rules/`)

| Working on… | Read |
|---|---|
| C# style, naming, async, errors, comments | `coding-csharp.md` |
| Module structure, boundaries, registration, DI, middleware order, config | `architecture.md` |
| Endpoints, CQRS, validation, exceptions, permissions, versioning, contract compatibility | `api-conventions.md` |
| EF Core, entities, migrations, tenant isolation, query filters | `database.md` |
| Cross-module events, Outbox/Inbox, idempotent handlers | `eventing.md` |
| Caching (HybridCache/Redis), keys, invalidation | `caching.md` |
| Background jobs (Hangfire), recurring jobs | `jobs.md` |
| Outbound HTTP resilience (Polly) | `resilience.md` |
| Files/blobs, presigned uploads, providers | `storage.md` |
| CORS, security headers, rate limiting, idempotency, quotas, front-end link origins | `security.md` |
| SignalR / SSE backend | `realtime.md` |
| Logging (levels, never-logged data), correlation, OpenTelemetry | `logging.md` |
| Localization (i18n), request culture, resource catalogs, localized exceptions | `localization.md` |
| Unit test conventions, what to test, NetArchTest | `testing.md` |
| Integration tests (Testcontainers harness + gotchas) | `integration-testing.md` |
| **Modifying `src/BuildingBlocks`** (read first — it's protected) | `buildingblocks-protection.md` |
| A specific module's quirks | `modules/{module}.md` (identity, multitenancy, chat, files, webhooks, auditing, billing, catalog, tickets, notifications) |

**Frontend** (`.claude/rules/frontend/`)

| Working on… | Read |
|---|---|
| TypeScript style, components, errors, naming | `coding-typescript.md` |
| Any React work (shared stack, API client, Query, Tailwind, design language) | `frontend/shared.md` |
| The operator app (`clients/admin`) | `frontend/admin.md` |
| The tenant app (`clients/dashboard`) | `frontend/dashboard.md` |

## Docs and tooling

- The build runs with `TreatWarningsAsErrors` and all analyzers on: a warning fails the build. Fix it; a suppression needs a `Justification`.
- Docs travel with the change (golden rule 10): `README.md` for what the product is and how to run it, `docs/` for how it works on the platform, the rules in `.claude/rules/` for how code is written here. Change the rule when you change the convention.
- Tools are pinned: the SDK in `global.json`, `dotnet-ef` in `.config/dotnet-tools.json`, packages in `src/Directory.Packages.props`.

## Adding things (quick pointers)

- **Feature** — Contracts command/query → handler → validator → endpoint → wire in module `MapEndpoints()` → tests. Details: `api-conventions.md`.
- **Module** — new `Modules.{Name}` + `.Contracts`, implement `IModule` w/ assembly-level `[assembly: TrislabModule(typeof(XModule), order)]`, register in **all four places**, add migration folder + tests. Details: `architecture.md`.
- **React page** — API module (`src/api/`) → English keys in `src/locales/en-US/{ns}.json` → page (`t()`) → register lazy route → (admin) mirror permission + RouteGuard → Playwright test. Details: `frontend/shared.md`.

## AI tooling resources

- **Rules** — `.claude/rules/**/*.md` (indexed above), each scoped with `paths:`. A rule without `paths:` would load in every session; `AgentFilesTests` fails on one.
- **Skills** — `.agents/skills/<name>/SKILL.md` is the skill. Claude Code finds skills only in `.claude/skills/`, so each also has a pointer `.claude/skills/<name>/SKILL.md` with the same front matter; add one with every new skill (`AgentFilesTests` checks it). Scaffolders: `add-feature`, `add-entity`, `add-module`, `add-react-page`, `add-full-slice`. Ops: `create-migration`, `add-integration-event`, `add-permission`, `release` (deploy, tag, promote, roll back). Reference, loaded by the agent only: `query-patterns`, `testing-guide`, `mediator-reference`.
- **Subagents** — `.claude/agents/*.md`, read-only: `code-reviewer` (the diff against the rules) and `architecture-guard` (boundaries, registration, `Architecture.Tests`). Run both before a commit.
- **Settings** — `.claude/settings.json` asks before an edit in `src/BuildingBlocks` or `.github/workflows`, and denies reading `.env` files.

## Working here

- **No AI attribution.** No `Co-Authored-By:` trailer and no "generated with" line in a commit, PR or comment.
- **Commit with your Trislab account**, never a personal one or another employer's.
- **The repository is public.** No secrets, customer names, prices or internal URLs in code, docs, PRs or commits.
- **Repetitive work is done by scripts.** When the same kind of work comes back and a script would make it faster and cheaper than doing it by hand, add one to `scripts/`, list it in `scripts/README.md`, and name it in the skill or doc that uses it. No script for one-off work. A script prints a summary and its errors, never a dump.
- Write "section N", never "§".
