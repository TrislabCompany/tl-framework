---
name: add-permission
description: Add a new permission end-to-end — server constant + endpoint gate. Use when a new endpoint needs authorization. The React apps that mirror it are in tl-product-saas. See modules/identity.md.
argument-hint: "[ModuleName] [Resource] [Action]"
---

# Add Permission

A permission is a server constant and an endpoint gate here. The operator console in `tl-product-saas` mirrors it by hand.

## Step 1 — Server constant (`Modules.{X}.Contracts/Authorization/{X}Permissions.cs`)

Add the constant to the resource group and ensure it's in the module's `All` collection. Convention:
`Permissions.{Resource}.{Action}`.

```csharp
public static class {X}Permissions
{
    public static class {Resources}
    {
        public const string View   = "Permissions.{Resources}.View";
        public const string Create = "Permissions.{Resources}.Create";   // ← new
    }
    public static IReadOnlyList<TrislabPermission> All { get; } = [ /* … include the new one … */ ];
}
```

The module already calls `PermissionConstants.Register({X}Permissions.All)` in `ConfigureServices`, so a new entry in `All` is picked up automatically.

## Step 2 — Gate the endpoint

```csharp
.RequirePermission({X}Permissions.{Resources}.Create);
```

⚠️ `RequiredPermissionAttribute` implements `IRequiredPermissionMetadata`. **Never let a second/duplicate of that interface exist** — it silently disables **all** `.RequirePermission()` gates app-wide. (See `.claude/rules/modules/identity.md`.)

## Step 3 — the React apps

The operator console and the tenant app live in `tl-product-saas`. Mirror the permission there: its permissions tree, the Role editor catalog, the route guard and the test seed. The framework has nothing to change.

## Verify

```bash
dotnet build src/App.slnx                       # 0 warnings (TreatWarningsAsErrors)
dotnet test src/Tests/Architecture.Tests        # boundaries, validators, tenant isolation
dotnet test src/Tests/{X}.Tests                 # the module's own tests
```

## Checklist

- [ ] Server constant added to `{X}Permissions` **and** its `All` collection
- [ ] Endpoint gated with `.RequirePermission(...)`; no duplicate `IRequiredPermissionMetadata`
- [ ] Build green
- [ ] The permission is mirrored in the React apps of `tl-product-saas` (a follow-up PR there)
