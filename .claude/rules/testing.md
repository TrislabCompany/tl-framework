---
paths:
  - "src/Tests/**"
---

# Testing conventions

Read before writing or changing tests.

## Stack

xUnit v3 (`xunit.v3.mtp-off`, runs on VSTest) · Shouldly (`result.ShouldBe(...)`) · NSubstitute (`Substitute.For<IService>()`) · AutoFixture (`_fixture.Create<T>()`) · NetArchTest (architecture rules) · Testcontainers (integration).

## xUnit v3 notes

- Test projects are executables (`<OutputType>Exe</OutputType>`). A new test project needs that plus the `xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` and `coverlet.collector` references; copy an existing csproj.
- `IAsyncLifetime` returns `ValueTask` and extends `IAsyncDisposable`: `public async ValueTask InitializeAsync()` / `public ValueTask DisposeAsync()`. A `WebApplicationFactory` fixture **overrides** `DisposeAsync()`, calls `base.DisposeAsync()` first, then disposes its containers.
- `xUnit1051` (pass `TestContext.Current.CancellationToken`) is off for `src/Tests` in `src/.editorconfig`. Its code fix also adds the token to NSubstitute setups and `Received()` checks, which changes what the mock matches. Pass the token by hand to long-running IO in a test when it should honour cancellation.

## Naming & shape

- Method name: `MethodName_Should_ExpectedBehavior_When_Condition`.
- Arrange-Act-Assert, grouped with `#region` (Happy Path / Exception / Edge Cases).
- Assert on observable behavior. When verifying a forwarded `CancellationToken`, assert the **specific** token (`Received(1).XAsync(arg, ct)`), not the implicit default — NSubstitute fills optional params with `default`, so `Received(1).XAsync(arg)` silently asserts `CancellationToken.None`.

## Test projects (`src/Tests/`)

| Project | Scope | Docker? |
|---|---|---|
| `{Module}.Tests` | Unit: handlers, services, domain | no |
| `Framework.Tests`, `Generic.Tests`, `Caching.Tests` | BuildingBlocks units | no |
| `Architecture.Tests` | NetArchTest: module boundaries + tenant-isolation rules + handler↔validator pairing | no |
| `Integration.Tests` | `WebApplicationFactory` over the test host, with real PostgreSQL/Redis/rustfs | **yes** |
| `Integration.Middleware.Tests` | Real middleware wiring, on the test host | **yes** |
| `TestHost` | Not a test project: the host the two integration projects run on, and that runs on its own for performance and security scans (`src/Tests/TestHost/README.md`) | no (standalone run: yes) |

```bash
pwsh scripts/affected-tests.ps1 -Run     # only what this branch affects (against origin/main): run this before a PR
dotnet test src/App.slnx                 # all (integration needs Docker): when the script says "Everything runs"
dotnet test src/Tests/{Module}.Tests             # one project
dotnet test --collect "XPlat Code Coverage" --settings coverage.runsettings
```

`ci / ci` runs the same script on a PR, and everything after the merge to `main`. The script picks the test projects through `ProjectReference`, and the integration-test areas (the folders of `Integration.Tests/Tests`) through `src/Tests/affected-map.json`. A new module or building block gets an entry there with the areas it needs, and a new area is named by the projects it tests; `AffectedMapTests` fails until both are done.

If Docker is down, integration tests fail fast with `DockerUnavailableException` — that is environmental, not a code regression. Run the unit projects to validate logic.

## Architecture tests (must stay green)

- Modules reference other modules only via `.Contracts`.
- Tenant-isolation rules on entities.
- Every command handler + paginated query handler has a `{Name}Validator` (`HandlerValidatorPairingTests`). Validator names accepted: `{Cmd}Validator`, `{Name}CommandValidator`, `{Name}Validator`.

## Integration tests

The harness and its gotchas (tenant context set inline, storage rewired after registration, long-polling for SignalR) are in `integration-testing.md`.

## What to test

- Every behaviour change gets a test that fails without it. A bug fix starts with the test that reproduces the bug.
- A handler: unit test for its logic and validator; an integration test when it touches the database, the HTTP pipeline or another module.
- Test names describe behaviour, not the implementation; one reason to fail per test.
- No sleeps, no real clock (`TimeProvider`), no order dependence between tests.
