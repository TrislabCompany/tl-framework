# Packages

How the framework is packed, versioned and used by a product. A product never copies framework code: it references these packages at one version and upgrades by raising it.

## 1. What is packed

Every project in `src/BuildingBlocks/` and `src/Modules/` is a NuGet package; nothing else is. Their own `Directory.Build.props` turn `IsPackable` on, and `src/Directory.Build.props` holds the metadata every package shares.

| Folder | Package id | Example |
| --- | --- | --- |
| `src/BuildingBlocks/<Name>` | `Trislab.Framework.<Name>` | `Trislab.Framework.Web`, `Trislab.Framework.Tenancy` |
| `src/Modules/<Name>/Modules.<Name>` | `Trislab.Modules.<Name>` | `Trislab.Modules.Identity` |
| `src/Modules/<Name>/Modules.<Name>.Contracts` | `Trislab.Modules.<Name>.Contracts` | `Trislab.Modules.Identity.Contracts` |

The package id is the project's `AssemblyName`. A project reference between two packed projects becomes a package dependency, so a product names only the modules it uses and gets their building blocks with them.

Never packed: the tests and the test host in `src/Tests/`. The framework holds only packages and tests; the hosts and samples live in `tl-product-saas`.

The npm packages are in `packages/`, one folder each, published as `@trislabcompany/<folder>`:

| Folder | Package | What it is |
| --- | --- | --- |
| `packages/framework-ui` | `@trislabcompany/framework-ui` | The shared React UI: the Trislab theme and the raw-colour check, i18n, the language switch, formatting by language, the Turnstile widget ([its README](../packages/framework-ui/README.md)) |

The React apps of the operator console and the tenant app live in `tl-product-saas` for now; they become `framework-admin` and `framework-dashboard` later.

- **Migrations** are compiled into their module, so they ship in its package. The migrator finds them in the package as it does in the project.
- **Mediator handlers** are found the same way: the host lists each module's assembly in `o.Assemblies`, and the source generator reads a package's assembly like a project's.
- **Debugging:** each package embeds its PDB and links its sources to the commit (Source Link), so a product can step into framework code.

To try the packages locally: `dotnet pack src/App.slnx -c Release -o <folder>`, then add `<folder>` as a package source in the consuming repository.

## 2. One version for everything

All packages share one version, the framework's: `Version` in `src/Directory.Build.props` for an untagged build, and the tag's version for a release. There is no version per package.

- **PATCH:** a fix; no change to a contract, endpoint or setting.
- **MINOR:** something new, or a change that stays compatible. A change that removes something goes in two steps: first it is marked obsolete, and the removal follows one MINOR later. A migration is always backward compatible ([architecture.md section 3](architecture.md#3-migrations)). **Not yet:** in development mode a MINOR may break anything, and the release notes list what breaks (`AGENTS.md`, "Development mode").
- **MAJOR:** reserved for a break that can't be done in two steps.

A release is a tag `vX.Y.Z` on `main`, created by an organization owner. The tag runs `.github/workflows/packages.yml`, which packs every package at the tag's version and publishes it to GitHub Packages: the NuGet packages, then each npm package in `packages/`. A version that is already published is skipped, so a re-run is safe. The first release is `v0.1.0`; a release candidate `vX.Y.Z-rc.<n>` publishes prerelease packages, and its npm packages go to the dist-tag `next`. A pull request publishes a prerelease too, for testing in a starter ([section 3](#3-using-the-packages-in-a-product), item 5).

## 3. Using the packages in a product

The starters `tl-product-saas` and `tl-product-webapp` are set up this way; a product created from one has it already.

1. **The feed.** A `nuget.config` names GitHub Packages, `https://nuget.pkg.github.com/TrislabCompany/index.json`, beside nuget.org. GitHub Packages asks for a token even for public packages: CI uses the workflow's token, Renovate the org secret of upgrades (below), and a developer a personal token with `read:packages`.
2. **The version.** `Directory.Packages.props` sets one property, `TrislabFrameworkVersion`, and every `Trislab.*` package uses it:

   ```xml
   <PropertyGroup>
     <TrislabFrameworkVersion>0.1.0</TrislabFrameworkVersion>
   </PropertyGroup>
   <ItemGroup>
     <PackageVersion Include="Trislab.Framework.Web" Version="$(TrislabFrameworkVersion)" />
     <PackageVersion Include="Trislab.Modules.Identity" Version="$(TrislabFrameworkVersion)" />
   </ItemGroup>
   ```

3. **The modules.** The host references the modules it registers, and registers each one in its `Program.cs` and the migrator's, as in this repository ([modules.md](modules.md)).
4. **Upgrades.**
   - **In the starters** (`tl-product-saas`, `tl-product-webapp`), `renovate.json` extends the preset `github>TrislabCompany/tl-framework//renovate/framework`. It opens one pull request per framework release `vX.Y.Z`, which raises `TrislabFrameworkVersion` and `@trislabcompany/framework-ui` together. The PR goes on auto-merge, and merges once a person approves it and `ci / ci` passes. It reads the NuGet version from the npm package `@trislabcompany/framework-ui`, not from the tag: the workflow `packages` publishes the npm package only after every NuGet package, so the PR opens only once the whole release can be restored. Keep the job `npm` after the job `nuget`. It moves the npm package with the npm manager, so the PR also updates `package-lock.json` or `pnpm-lock.yaml`, and CI's frozen install passes. For that the npm manager reads GitHub Packages with a classic token with `read:packages`, which an org owner keeps as the org secret `TRISLAB_PACKAGES_TOKEN` in the Mend developer portal (developer.mend.io, the organisation's Settings, Credentials). The release's "Breaking changes for the starters" say what else the PR has to change.
   - **In a product repository,** the preset does nothing. The starter's template update brings the framework version it was tested with, together with the host changes it needs, one MINOR at a time (tl-product-ideas D-40).
5. **Testing a pull request in a starter.** Every pull request from a branch of this repository that changes a package publishes a prerelease of all packages (`.github/workflows/pr-packages.yml`): the next MINOR after the newest release, then the pull request and the run, `0.7.0-pr.54.3` after `v0.6.0`. Its npm packages go to the dist-tag `pr-<number>`, and the starters' Renovate preset skips `-pr.` versions, so nothing takes one by itself. A starter's pull request that needs the change opens at the same time:
   1. Pin both to the prerelease the run's summary names: `TrislabFrameworkVersion` in `src/Directory.Packages.props`, and `npm install @trislabcompany/framework-ui@<version> --save-exact` in each app.
   2. Say `Depends on TrislabCompany/tl-framework#<number>` in its description; each push to the framework pull request publishes a new run, so pin the newest one.
   3. Once the framework pull request is merged and released, pin the release in the same pull request, then merge. A starter never merges on a prerelease.

## 4. Adding a package

A new building block or module under `src/BuildingBlocks/` or `src/Modules/` is packed by itself. Give it an `AssemblyName` in the `Trislab.Framework.*` or `Trislab.Modules.*` form, and run `dotnet pack src/App.slnx` before you push: a warning fails the pack as it fails the build.

## 5. Vulnerable dependencies

Every restore audits every NuGet package, direct and transitive, against the known advisories of every severity (`NuGetAuditMode` `all`, `NuGetAuditLevel` `low` in `src/Directory.Build.props`).

- **A finding is a warning, not an error.** The build treats every other warning as an error, but NU1901 to NU1904 stay warnings, so an advisory published today doesn't fail a PR that changes something else.
- **It is fixed in its own PR:** raise the package, or the transitive one with a `PackageVersion` in `src/Directory.Packages.props`. Renovate opens that PR by itself for a security update.
- **The daily audit fails on it.** `.github/workflows/audit.yml` runs every day: it audits the NuGet packages of `src/App.slnx` and the npm packages in `packages/`, and fails on an advisory of moderate severity or higher. A failure opens the issue "main is failing: audit" (label `ci-main`), and the next green run closes it.
- **An advisory that doesn't apply** is accepted in `.audit-accepted`: its URL or GHSA id, with a line `# reason: <text>; expires: YYYY-MM-DD` above it. An entry past its date fails the audit again.

## 6. What a release proves

`packages.yml` adds two things to every release:

- **Build provenance** for each `.nupkg` and `.tgz`: a signed record of the workflow run and the commit that built it. A product checks a package it downloaded with `gh attestation verify <file> --repo TrislabCompany/tl-framework`.
- **SBOMs** (software bills of materials, in CycloneDX JSON): one of the NuGet packages, `trislab-framework-<version>.nuget.cdx.json`, and one of each npm package, `<name>-<version>.npm.cdx.json`. They list every dependency a product gets with the framework, and are attached to the GitHub Release of the tag. When the release doesn't exist yet as the workflow runs, they stay in the run's artifact, and a re-run of the job `sbom` attaches them.
