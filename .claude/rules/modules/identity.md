---
paths:
  - "src/Modules/Identity/**"
  - "src/Tests/Identity.Tests/**"
---

# Module: Identity

Auth (JWT + ASP.NET Identity), users, roles, permissions, sessions, impersonation, 2FA.

## Service shape

`IUserService` is a **facade** that delegates to focused single-responsibility services — change behavior in the specific service, not the facade:

| Interface | Concern |
|---|---|
| `IUserRegistrationService` | register, external-principal create, email/phone confirm |
| `IUserProfileService` | get/list/count, update profile, image, existence checks |
| `IUserStatusService` | activate/deactivate (`DeleteAsync` == deactivate), audited toggles |
| `IUserRoleService` | role assignment, admin-role guards |
| `IUserPasswordService` | forgot/reset/change password, history + expiry |
| `IUserPermissionService` | effective permissions, cache invalidation |

`ChangePassword`/`Update`/`Delete` etc. flow facade → service → EF/UserManager. `CancellationToken` is `= default` on these interfaces and propagated into EF sinks (note: `UserManager`/`RoleManager` have no CT overloads, so private helpers that only call them don't take one).

## Permission gating footgun

`RequiredPermissionAttribute` implements `Trislab.Framework.Shared.Identity.Authorization.IRequiredPermissionMetadata`. **Never let a second/duplicate `IRequiredPermissionMetadata` appear** — it silently disables **all** `.RequirePermission()` gates across the app. Permission constants live in `Shared/Identity/*Permissions.cs`.

## Hosted services (background)

- `RolePermissionSyncHostedService` — best-effort sync of the permission catalog; loops, catches `Exception` *with* an `OperationCanceledException` filter, logs and continues.
  The sync is authoritative for the built-in `Basic` and `Admin` roles: it adds missing permissions **and removes revoked ones** (root `Admin` keeps `Root` permissions), logging each removal. `PermissionSyncOptions:RemoveRevoked=false` makes it add-only — use it for hosts that load only some modules. Custom roles are never touched.
- **A picker needs `Users.Pick`, never `Users.View`.** `Users.View` (the user list, search and details) is an Admin permission. `Users.Pick` is Basic and opens only `GET /identity/users/lookup`: active users, by `search` (first name, last name or user name, never the e-mail) or by `ids` (to show names), returning `UserLookupDto` (id, user name, names, picture). A search needs at least 2 characters and matches names and user names, never the e-mail, so a product must not use the e-mail as the user name. Ids are repeated parameters (`?ids=a&ids=b`). A Basic user gets 403 from `GET /users/{id}` too: give a new picker or a "show this user's name" feature the lookup, not the list.
- **Root-only permissions count only in the root tenant.** `RequiredPermissionAuthorizationHandler` refuses an endpoint that needs an `IsRoot` permission when the caller's tenant is not root, even if a custom role still holds it (a grant made before the permission became root-only). `RoleService.FilterRootPermissions` stops new grants of that kind.
- `SessionCleanupHostedService` — hourly expired-session purge; OCE handled by a preceding catch.

These are the model for background loops: stay alive, log with context, never swallow cancellation. See `api-conventions.md`.

## Tokens / sessions

Login `POST /api/v1/identity/token/issue` (header `X-Trislab-App` enforces the app boundary: a root-tenant account gets no token for `dashboard`, and any other tenant's account gets none for `admin`, the operator console). **Tenant header:** in multi-tenant mode the anonymous endpoints (issue, refresh, forgot and reset password, self-register) need the `tenant` header (`RequireTenantHeaderWhenMultitenant()` answers 400 without it); in single-tenant mode they need none, the server resolves the fixed tenant, a header is ignored, and the token still carries the `tenant` claim, so claim readers don't change. The app boundary check applies in multi-tenant mode only. Refresh `POST /api/v1/identity/token/refresh` cross-checks subject. Session rows are written best-effort during login — failures log a warning and login still succeeds. Admin can't demote/deactivate the last admin or the root-tenant seed admin (guards in `UserRoleService`/`UserStatusService`).

## Tests

`Identity.Tests` is the largest unit suite. When asserting a forwarded `CancellationToken`, assert the specific token (see `testing.md`).
