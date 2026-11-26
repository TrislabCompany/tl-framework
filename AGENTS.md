# This repository

> `tl-framework`: the framework every Trislab SaaS product and webapp is built on. The packages of a modular
> .NET 11 monolith and a React 19 UI package, with their tests, derived from an open-source starter kit and
> adapted to the platform contract (`README.md`, `docs/`). It holds only packages and tests: no app, no host.

## Role

The same block starts the `AGENTS.md` of every Trislab repository, so an agent in any of them knows where a change belongs.

- **For:** the framework of the Trislab SaaS products and webapps: the core building blocks and every reusable module, released as packages that products reference.
- **Contains:**
  - the core building blocks in `src/BuildingBlocks/` (`Trislab.Framework.*`), among them `Tenancy`, which resolves the tenant and owns the event, outbox and job tenant scopes in both modes;
  - every reusable module in `src/Modules/`, with its `.Contracts` (`Trislab.Modules.*`) and its own migrations (each declares the tenancy modes it supports, multi-tenant, single-tenant or both);
  - country rule packs (`*-si`), the first being `Modules.InvoiceDocuments.Si`;
  - the React package `packages/framework-ui` (later `framework-admin` and `framework-dashboard`);
  - the framework's agent skills, and `docs/upstream.md`, the log of the upstream ports.
  - the test host `src/Tests/TestHost`, a small host of the framework's modules that the integration and security tests run against;
- **Never contains:** a product's own features; Trislab-only code; customer names, secrets or prices; live infrastructure.
- **A change goes elsewhere when it is:**
  - starter scaffolding (the host, the migrator, the AppHost, the React apps, `init-product`, the template manifest, the samples, the app's images, `platform.yaml` and its deploy and release workflows): `tl-product-saas` or `tl-product-webapp`;
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

The **packages** of a modular monolith (Vertical Slice Architecture) backend and a React UI package, plus the
tests that guard them. Multitenancy, auth, auditing, billing, files and more are first-class modules. The
apps built from them (the hosts, the React apps, the samples) live in `tl-product-saas` and
`tl-product-webapp`, never here.

- **Backend** — .NET 11, EF Core 11, PostgreSQL, Redis, JWT + ASP.NET Identity, Finbuckle multitenancy,
  Hangfire, OpenAPI/Scalar, Serilog + OpenTelemetry.
- **Frontend** — `packages/framework-ui` (React 19, TypeScript 6, i18next).

## Repo map

| Path | What |
|------|------|
| `src/BuildingBlocks/` | Shared framework libraries (Core, Persistence, Web, Caching, Eventing, Storage, Quota, Tenancy…). **Protected — see below.** |
| `src/Modules/{Name}/` | Bounded contexts. Each has a runtime project + a `.Contracts` project (its only public API), and ships its own EF migrations in `Migrations/`. |
| `src/Tests/` | Per-module tests, `Architecture.Tests` (NetArchTest), `Integration.Tests` (Testcontainers). |
| `src/Tests/TestHost` | The test host: the framework's modules plus one tiny test module, composed like a product's host. `Integration.Tests` and `Integration.Middleware.Tests` run on it in process; it also runs on its own (`compose.yaml`) for performance and security scans. Never packed or deployed. |
| `tests/security` | The security checks of the test host: the endpoint-authorization test (in `Integration.Tests`), the ZAP scan of the running host on every pull request, and the findings accepted with a reason (`tests/security/README.md`). |
| `tests/perf` | The k6 performance scripts: a smoke run on every pull request, a load run every night, both against the test host as a container (`tests/perf/README.md`). |
| `packages/framework-ui` | The npm package `@trislabcompany/framework-ui`: the Trislab theme (CSS variables, Inter, `framework-ui-check-colors`), i18n, the language switch, formatting by language, the Turnstile widget. `npm test` runs in `ci / ci`. |
| `.github/workflows/ci.yml` | The `ci / ci` check. A PR runs the tests it affects (`scripts/affected-tests.ps1`), lints the changed Markdown and reviews new dependencies; a docs-only PR only lints. A push to `main` runs every test, and a failure there opens the issue "main is failing: ci / ci" (label `ci-main`); check the open ones before starting work. |
| `.github/workflows/packages.yml` | On a release tag, publishes the NuGet and npm packages to GitHub Packages, with build provenance and SBOMs (`docs/packages.md`). |
| `.github/workflows/audit.yml` | Every day, audits the NuGet and npm dependencies; a failure opens the issue "main is failing: audit" (`docs/packages.md` section 5). |
| `scripts/` | Scripts for repetitive work, listed in `scripts/README.md` (porting upstream commits). |
| `docs/` | How the repository works: getting started, architecture, modules, packages, bot protection, the upstream log. |
| `renovate/framework.json` | The Renovate preset the starters extend: one PR per framework release, raising `TrislabFrameworkVersion` and `framework-ui` together; it does nothing in a product repository (`docs/packages.md`). |

## Tech stack

| Backend | | Frontend | |
|---|---|---|---|
| Framework | .NET 11 / C# latest | Framework | React 19 + Vite 8 + TS 6.x |
| CQRS | Mediator 3.x (source-gen) | Data | TanStack Query v5 |
| Validation | FluentValidation 12.x | Routing | React Router 7 |
| ORM / DB | EF Core 11 / PostgreSQL (Npgsql) | UI | Radix + Tailwind v4 + CVA (shadcn) |
| Auth | JWT Bearer + ASP.NET Identity | Forms | react-hook-form + zod (**admin only**) |
| Multitenancy | Finbuckle 10.x | Realtime | SSE, SignalR (backend) |
| Cache / Jobs | Redis, Hangfire | Tests | Vitest, in Node and in Chromium with Playwright (`packages/framework-ui`) |
| Docs | OpenAPI + Scalar | | |
| Testing | xUnit v3, Shouldly, NSubstitute, AutoFixture, NetArchTest, Testcontainers | | |

## Build & run

```bash
dotnet build src/App.slnx    # build the packages and the tests
pwsh scripts/affected-tests.ps1 -Run   # the tests this branch affects; run before every PR
dotnet test src/App.slnx     # all tests; integration tests REQUIRE Docker
cd packages/framework-ui && npm ci && npm test   # the npm package
```

There is no app to run here. The test host (`src/Tests/TestHost`) runs the framework's modules on their own for
the integration, performance and security tests; a product runs them in its own host (`tl-product-saas`).

## Git and review

- Branch from `main` with a short kebab-case topic (`billing-proration`). One change per PR; a workflow change goes in its own PR.
- The PR title is an imperative plain sentence of 72 characters or fewer; it becomes the squash commit. The body follows `.github/pull_request_template.md`.
- A PR merges with the check `ci / ci` green and one code owner's approval, never your own. The author resolves the threads.
- Releases are tags `vX.Y.Z`, created only by an organization owner and never moved; `packages.yml` publishes the packages (`docs/packages.md`). The framework has no app, so there is no deploy or release workflow for images here.
- Before asking for review, run the `code-reviewer` and `architecture-guard` subagents. A review checks: correctness and edge cases, a test for each behaviour change, logs per `logging.md`, contracts compatible per `api-conventions.md`, docs updated, no secrets.

## Development mode: breaking changes are allowed

The framework, the starters and the products are still in development. No product runs in production, and there is no real data to keep. Until Trislab ends this mode here, it overrides every compatibility rule in this repository (the platform contract below, `api-conventions.md`, `buildingblocks-protection.md`, `docs/packages.md`, `docs/architecture.md`):

- **Change in place.** Rename or remove an endpoint, a field, a contract, an event, a setting or a public type when that gives the better design. No `v2` endpoint or event, no `[Obsolete]` step, no expand and contract.
- **Migrations may break.** A migration may drop, rename or reshape columns and tables, with no data migration. Add a new migration; never edit one that is already on `main`.
- **Say what breaks.** The PR body lists every breaking change under "Breaking changes for the starters", with what a starter or product has to change. The starters adopt them when they upgrade.
- **What doesn't change:** tests for every behaviour, the security rules, the module boundaries and the golden rules below.

The mode ends when the first product goes to production. Then this section is removed and the compatibility rules apply again.

## Platform contract (do not break)

The apps are built in the starters, but they rely on what the packages do, so keep these true:

- A package reads its settings only as options; the platform's names (`DATABASE_URL`, `REDIS_URL`, `S3_*`, `JWT_SIGNING_KEY`, …) are mapped onto the options by the product's host, never read by a package.
- Migrations are backward compatible: the previous API revision keeps serving on the migrated database. Not yet: during development mode breaking changes are allowed (`AGENTS.md`, "Development mode").

## Golden rules (do not break)

1. **Module boundaries** — a module references another module only through its `.Contracts` project, never its runtime project. Enforced by `Architecture.Tests`.
2. **A product registers a module in FOUR places** — its API host's `Program.cs` Mediator `o.Assemblies` (two markers each) + `moduleAssemblies` array, **and the identical pair in its migrator's `Program.cs`** (both in the starter; `src/Tests/TestHost` does the same for the tests). A missing Mediator marker = handlers silently undiscovered. See `architecture.md`.
3. **Tenant isolation is default-ON** via `BaseDbContext`. Opt out only via `IGlobalEntity`. Subclass DbContexts call `base.OnModelCreating` **last**. See `database.md`.
4. **Do NOT modify `src/BuildingBlocks`** without explicit approval — shared by every module, wide blast radius.
5. **Mediator handlers must be `public sealed`**, return `ValueTask<T>`, and `.ConfigureAwait(false)` every await.
6. **Structured logging only** — no string interpolation in log messages; use message templates / `[LoggerMessage]`.
7. **Propagate `CancellationToken`** into every EF/IO call; add as `= default` on public service methods.
8. **Every command handler + paginated query handler needs a validator** (`{Name}Validator`). Enforced by `Architecture.Tests`.
9. **Docs travel with the change** — a change to a setting, a module, an endpoint or a package isn't done until `README.md` and `docs/` match it.

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
| A specific module's quirks | `modules/{module}.md` (identity, multitenancy, files, webhooks, auditing, subscriptions-usage, invoice-documents, invoice-documents-si, notifications) |

**Frontend** (`packages/`)

| Working on… | Read |
|---|---|
| TypeScript style, components, errors, naming | `coding-typescript.md` |

## Docs and tooling

- The build runs with `TreatWarningsAsErrors` and all analyzers on: a warning fails the build. Fix it; a suppression needs a `Justification`.
- Docs travel with the change (golden rule 9): `README.md` for what the framework is and how to use it, `docs/` for how it works, the rules in `.claude/rules/` for how code is written here. Change the rule when you change the convention.
- Tools are pinned: the SDK in `global.json`, `dotnet-ef` in `.config/dotnet-tools.json`, packages in `src/Directory.Packages.props`.

## Adding things (quick pointers)

- **Feature** — Contracts command/query → handler → validator → endpoint → wire in module `MapEndpoints()` → tests. Details: `api-conventions.md`.
- **Module** — new `Modules.{Name}` + `.Contracts`, implement `IModule` w/ assembly-level `[assembly: TrislabModule(typeof(XModule), order)]`, register in **all four places**, add migration folder + tests. Details: `architecture.md`.

## AI tooling resources

- **Rules** — `.claude/rules/**/*.md` (indexed above), each scoped with `paths:`. A rule without `paths:` would load in every session; `AgentFilesTests` fails on one.
- **Skills** — `.agents/skills/<name>/SKILL.md` is the skill. Claude Code finds skills only in `.claude/skills/`, so each also has a pointer `.claude/skills/<name>/SKILL.md` with the same front matter; add one with every new skill (`AgentFilesTests` checks it). Scaffolders: `add-feature`, `add-entity`, `add-module`. Ops: `create-migration`, `add-integration-event`, `add-permission`. Reference, loaded by the agent only: `query-patterns`, `testing-guide`, `mediator-reference`.
- **Subagents** — `.claude/agents/*.md`, read-only: `code-reviewer` (the diff against the rules) and `architecture-guard` (boundaries, registration, `Architecture.Tests`). Run both before a commit.
- **Settings** — `.claude/settings.json` asks before an edit in `src/BuildingBlocks` or `.github/workflows`, and denies reading `.env` files.

## Working here

- **No AI attribution.** No `Co-Authored-By:` trailer and no "generated with" line in a commit, PR or comment.
- **Commit with your Trislab account**, never a personal one or another employer's.
- **The repository is public.** No secrets, customer names, prices or internal URLs in code, docs, PRs or commits.
- **Repetitive work is done by scripts.** When the same kind of work comes back and a script would make it faster and cheaper than doing it by hand, add one to `scripts/`, list it in `scripts/README.md`, and name it in the skill or doc that uses it. No script for one-off work. A script prints a summary and its errors, never a dump.
- Write "section N", never "§".
