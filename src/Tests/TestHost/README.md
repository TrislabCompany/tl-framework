# Test host

A small host of the framework's modules, built like a product's host. The framework holds packages and
tests, not an app, so its tests need a host of their own: this one.

It serves three kinds of test:

- **Integration and security tests, in process.** `Integration.Tests` and `Integration.Middleware.Tests` start it
  with `WebApplicationFactory<Program>` over Testcontainers (PostgreSQL, rustfs), in both tenancy modes.
- **Performance tests (k6).** Run it as a container and point k6 at it (`tests/perf/README.md`).
- **Security scans (ZAP), later.** Run it on its own and point ZAP at it.

It is **never deployed and never packed.** It signs in with a fixed development key and has endpoints that
exist only for tests.

## What is in it

- Every framework module: Identity, Multitenancy, Auditing, Files, Webhooks, SubscriptionsUsage,
  InvoiceDocuments, Notifications. In single-tenant mode the modules that need a tenant catalog drop out
  (`ModuleLoader.SelectSupported`).
- One tiny test module (`Module/`), which gives the framework's tests what a product's modules would:

| Part | For |
| --- | --- |
| `TestItem`, `TestDbContext` (schema `testhost`), one migration | a tenant-scoped entity: the eventing, outbox and storage tests |
| `TestItemFileAccessPolicy` (owner type `TestItem`), `TestItemFileStorageKeyChangedHandler` | the Files policies and the rewrite of a stored URL when a file moves |
| `POST /api/v1/test/notifications` | publishing `UserNotificationRequestedIntegrationEvent`, the way a module notifies a user |
| `TestChannelMembership`, `/api/v1/test/channels` | the realtime hub's membership checks (presence, typing) |

- `Provisioning/TestHostProvisioning`: migrate, seed and sync the roles, like a product's migrator. The tests
  call it from their factories; a standalone run calls it at startup when `TestHost:Provision` is `true`.

The host reads plain configuration (`appsettings*.json`, environment variables). It does not map the platform's
variables (`DATABASE_URL`, `S3_*`): that belongs to a product's host.

## Run it on its own

```bash
docker compose -f src/Tests/TestHost/compose.yaml up -d   # Postgres on 5433, rustfs on 9100
dotnet run --project src/Tests/TestHost --launch-profile http
```

The `http` profile sets `TestHost__Provision=true`, so the host creates the bucket, migrates the databases and
seeds the root tenant, then serves on http://localhost:5040 (`/healthz`, `/readyz`, `/scalar`). The seeded
administrator is `admin@root.com`; the password is `Seed:DefaultAdminPassword` in `appsettings.Development.json`.
For single-tenant mode, pass `--TenancyOptions:Mode=SingleTenant` and the settings under
`TenancyOptions:SingleTenant` (`Id`, `Name`, `Country`, `AdminEmail`).

## Run it as a container

`Dockerfile` publishes the host into the same kind of image as the API's: chiseled ASP.NET runtime, non-root user,
port 8080, base images pinned by digest. `Dockerfile.dockerignore` keeps the host in the build context (the
repository's `.dockerignore` leaves out `src/Tests`). The `host` service of `compose.yaml`, behind the profile
`container`, runs it with the two services above, in the development configuration (signing key, seed password):

```bash
pwsh scripts/test-host.ps1 up      # build, start, wait for /readyz; the host is on http://localhost:5040
pwsh scripts/test-host.ps1 token   # an access token of admin@root.com
pwsh scripts/test-host.ps1 down
```

`-Port` (or `TEST_HOST_PORT` for compose) moves the published port; on Windows, where port 5040 belongs to a system
service, use 5041.
