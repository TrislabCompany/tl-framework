---
paths:
  - "src/Tests/Integration.Tests/**"
  - "src/Tests/Integration.Middleware.Tests/**"
---

# Integration testing

`src/Tests/Integration.Tests/` + `Integration.Middleware.Tests/`. Read before writing tests that touch the DB/HTTP pipeline. See `testing.md` for unit conventions.

## Harness

`WebApplicationFactory<Program>` over the **test host** (`src/Tests/TestHost`), on **real** infra via Testcontainers — PostgreSQL + Redis + rustfs. **Docker must be running**; if it isn't, tests fail fast with `DockerUnavailableException` (environmental, not a regression — run the unit projects instead).

The test host composes the framework's modules like a product's host does, plus one tiny test module (`TestHost/Module/`): a tenant-scoped entity with its own DbContext (`TestItem`), a file access policy for the owner type `TestItem`, `POST /api/v1/test/notifications` (publishes `UserNotificationRequestedIntegrationEvent`), and in-memory channels for the realtime hub (`/api/v1/test/channels`). A framework test that needs an entity, a policy, a notification or a channel uses these; it never needs a sample or a product module, and the framework's tests never reference product code. `TestHostProvisioning` does what a product's migrator does (migrate, seed, role sync) for the root tenant, or the fixed tenant in single-tenant mode; the factories call it.

`TrislabWebApplicationFactory` (`Integration.Tests/Infrastructure/`) boots the containers, overlays in-memory config, swaps `IMailService` → `NoOpMailService`, and rewires storage to rustfs, including the keyed presign client.

## Must-know gotchas

- **Tenant context is AsyncLocal — set it inline.** Set the Finbuckle tenant context **in the same method** as the `UserManager`/`DbContext` call. Setting it in an awaited helper loses it across the async boundary → NRE in the tenant query filter.
- **Storage is wired eagerly.** `AddTrislabStorage` reads `Storage:Provider` before the test config overlay, so it picks `LocalStorageService`. The factory **removes the `IStorageService`/`LocalStorageService`/`S3StorageService` descriptors post-registration and re-registers the S3 stack** at rustfs. Follow that when a test needs real object storage. (See `storage.md`.)
- **SignalR tests force long-polling** — TestServer has no WebSocket. Configure the client transport accordingly.
- **Rate limiting is read eagerly** — `Integration.Middleware.Tests` sets `RateLimitingOptions:Enabled` via env var **before** host build, since flipping it after has no effect.
- **Test order is not guaranteed** (xUnit v3 orders differently from v2, and it shifted several hidden dependencies). Clean up shared state you create. Tenants pointing at an unreachable database go through `_factory.RemoveTenantsAsync(ids)` in `DisposeAsync`, or all-tenant jobs run by later tests count them as failures. Don't assume a new tenant's audit rows fit on page 1, because provisioning writes its seed data as `EntityChange` rows.
- **`WithWebHostBuilder` hosts share two statics with the shared host**: Hangfire's `JobStorage.Current` and the static `Audit` publisher. `PrimaryHostStaticsRestorer` points both back at the shared host when a derived host stops. Always dispose derived factories (`await using`).

## Endpoint authorization

`Tests/Security/EndpointAuthorizationTests.cs` fails when an endpoint is public (`AllowAnonymous()`) and not in its `AnonymousEndpoints` list, when a listed one no longer exists, or when an endpoint answers a request without a token with anything but `401`. Making an endpoint public means adding `AllowAnonymous()` and a list entry with the reason, in the same pull request (`tests/security/README.md`).

## Coverage

```bash
dotnet test --collect "XPlat Code Coverage" --settings coverage.runsettings
```
Cobertura; includes `[Trislab.Modules.*]` + `[Trislab.Framework.*]`; excludes tests, every `Migrations` namespace, and `*HostedService`.
