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

Never packed: the hosts in `src/Host/`, the samples in `src/Samples/` (they move to `tl-product-saas`), and the tests.

The npm packages are in `packages/`, one folder each, published as `@trislabcompany/<folder>`:

| Folder | Package | What it is |
| --- | --- | --- |
| `packages/framework-ui` | `@trislabcompany/framework-ui` | The shared React UI: i18n, the language switch, formatting by language, the Turnstile widget ([its README](../packages/framework-ui/README.md)) |

The React apps `clients/admin` and `clients/dashboard` are not packages yet; they become `framework-admin` and `framework-dashboard` later.

- **Migrations** are compiled into their module, so they ship in its package. The migrator finds them in the package as it does in the project.
- **Mediator handlers** are found the same way: the host lists each module's assembly in `o.Assemblies`, and the source generator reads a package's assembly like a project's.
- **Debugging:** each package embeds its PDB and links its sources to the commit (Source Link), so a product can step into framework code.

To try the packages locally: `dotnet pack src/App.slnx -c Release -o <folder>`, then add `<folder>` as a package source in the consuming repository.

## 2. One version for everything

All packages share one version, the framework's: `Version` in `src/Directory.Build.props` for an untagged build, and the tag's version for a release. There is no version per package.

- **PATCH:** a fix; no change to a contract, endpoint or setting.
- **MINOR:** something new, or a change that stays compatible. A change that removes something goes in two steps: first it is marked obsolete, and the removal follows one MINOR later. A migration is always backward compatible ([deployment.md section 3](deployment.md#3-migrations)).
- **MAJOR:** reserved for a break that can't be done in two steps.

A release is a tag `vX.Y.Z` on `main`, created by an organization owner (the `release` skill). The tag runs `.github/workflows/packages.yml`, which packs every package at the tag's version and publishes it to GitHub Packages: the NuGet packages, then each npm package in `packages/`. A version that is already published is skipped, so a re-run is safe. `release.yml` runs beside it for the images. The first release is `v0.1.0`; a release candidate `vX.Y.Z-rc.<n>` publishes prerelease packages, and its npm packages go to the dist-tag `next`.

## 3. Using the packages in a product

The starters `tl-product-saas` and `tl-product-webapp` are set up this way; a product created from one has it already.

1. **The feed.** A `nuget.config` names GitHub Packages, `https://nuget.pkg.github.com/TrislabCompany/index.json`, beside nuget.org. GitHub Packages asks for a token even for public packages: CI uses the workflow's token, Renovate its own, and a developer a personal token with `read:packages`.
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
4. **Upgrades.** `renovate.json` extends the preset `github>TrislabCompany/tl-framework//renovate/framework`. It puts every `Trislab.*` package into one pull request per framework version, merges a PATCH by itself once CI passes, and leaves a MINOR or MAJOR for review. Take every MINOR in order, without skipping one.

## 4. Adding a package

A new building block or module under `src/BuildingBlocks/` or `src/Modules/` is packed by itself. Give it an `AssemblyName` in the `Trislab.Framework.*` or `Trislab.Modules.*` form, and run `dotnet pack src/App.slnx` before you push: a warning fails the pack as it fails the build.
