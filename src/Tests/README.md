# Architecture Tests

This folder contains solution-wide architecture tests for the Trislab .NET 10 Starter Kit. The goal is to automatically enforce layering, dependency, and naming rules as the codebase evolves.

## Project

- `Architecture.Tests` targets `net10.0` and lives under `src/Tests/Architecture.Tests`.
- Test dependencies are limited to xUnit v3 (`xunit.v3.mtp-off`), `Shouldly`, and `AutoFixture`, with versions defined in `src/Directory.Packages.props`.

## The other test projects

`Integration.Tests` and `Integration.Middleware.Tests` run on `src/Tests/TestHost`, a small host of the framework's modules (see its `README.md`). `Architecture.Tests` references the module projects directly, and `ModuleAssemblyDiscoveryGuardTests` fails when a module under `src/Modules` is missing from its references, so no module drops out of the rules silently.

## What Is Covered

- **Module dependencies**: module runtime projects (`Modules.*`) cannot reference other modules' runtime projects directly; only their own runtime, contracts, and building blocks are allowed (validated via csproj inspection).
- **Feature layering**: feature types under `Modules.*.Features.v{version}` are checked with NetArchTest to depend only on allowed layers (System/Microsoft, `Trislab.Framework.*`, their module, and module contracts).
- **Host boundaries**: module code must not depend on host applications (the test host included), and the test host must not depend directly on module feature or data internals.
- **Namespace conventions**: selected areas (for example, `BuildingBlocks/Core/Domain`) must declare namespaces that reflect the folder structure.

## Running the Tests

- Run the tests a branch affects: `pwsh scripts/affected-tests.ps1 -Run`; it prints the plan without `-Run`.
- Run all tests (including architecture tests): `dotnet test src/App.slnx`.
- `AffectedMapTests` keeps `src/Tests/affected-map.json` complete: every building block and module project has its integration-test areas, and every area is named by a project.
- Architecture tests are lightweight and rely only on project and file structure; they do not require any external services or databases.

## Extending the Rules

- Add new rules as additional test classes inside `Architecture.Tests`, following the existing patterns (using NetArchTest for type-level rules and reflection or project file inspection where appropriate).
- Keep rules fast and deterministic; avoid environment-specific assumptions so the tests remain stable in CI.
