# Getting started

How to build the framework and run its tests. The framework holds only packages and tests: there is no app to run here. To run a product, use a starter, `tl-product-saas` or `tl-product-webapp`.

## 1. What you need

| Tool | Version | For |
| --- | --- | --- |
| Docker | Any current version | The integration tests' throw-away containers (Postgres, S3-compatible storage), and the test host on its own |
| .NET SDK | The version in `global.json` | The packages and the tests |
| Node.js | 22, with npm | The npm packages in `packages/` |

A ready development container with all three is in `.devcontainer/`.

## 2. Build and test

```sh
dotnet build src/App.slnx
dotnet test src/App.slnx
npm --prefix packages/framework-ui ci
npm --prefix packages/framework-ui test
```

These are the commands `ci / ci` runs. Docker must run: the integration tests start their own containers.

| Tests | Where | What they check |
| --- | --- | --- |
| Architecture tests | `src/Tests/Architecture.Tests` | The module boundaries, the conventions ([modules.md](modules.md)) and the agent files |
| Unit tests | `src/Tests/<Module>.Tests`, `Framework.Tests`, `Caching.Tests`, `Generic.Tests` | Each module and building block without a database |
| Integration tests | `src/Tests/Integration.Tests`, `Integration.Middleware.Tests` | The endpoints of the framework's modules against a real Postgres 17 and S3-compatible storage, in both tenancy modes, on the test host |
| npm package tests | `packages/<name>` | `npm test`: type check and Vitest |

## 3. The test host

The integration tests run on `src/Tests/TestHost`, a small host of the framework's modules built like a product's host. To run it on its own, see [its README](../src/Tests/TestHost/README.md). It is never deployed and never packed.

## 4. Pack the packages

`dotnet pack src/App.slnx -c Release -o <folder>` packs every NuGet package; run it before a PR that adds or renames a package ([packages.md](packages.md#4-adding-a-package)).
