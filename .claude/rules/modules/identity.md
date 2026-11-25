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

## Global users and memberships

- **`TrislabUser` is global** (not multi-tenant): one account per e-mail, unique `NormalizedEmail` and `NormalizedUserName`. Its claims, logins, tokens (the authenticator key), passkeys and `PasswordHistory` are global too. `TenantMembership` (user, tenant, `IsActive`) is tenant-scoped, as are roles, role claims, user roles, groups and sessions. `IdentityDbContext` derives from Identity's `IdentityDbContext` and implements `IMultiTenantDbContext` itself; it always uses the main database.
- **The query filter `IdentityDbContext.MembershipFilter`** limits `Users` (so `UserManager.Users` and `FindByIdAsync`) to the members of the current tenant, active or not. "Active" is the membership's: use `u.Memberships.Any(m => m.IsActive)` (the navigation is tenant-filtered) or `db.ActiveMembers`. Never add an `IsActive` to the user.
- **`TrislabUserStore`** makes `FindByEmailAsync` and `FindByNameAsync` global, so Identity's uniqueness checks see every account. They return an account, not a member: check the membership before acting in the tenant. Account-level code (sign-in, password reset, the avatar handler) reads users with `IgnoreQueryFilters([IdentityDbContext.MembershipFilter])`.
- **Root members are operators** and have no other membership: `UserRegistrationService.AddMembershipAsync` and the seeding refuse it both ways. Operators reach other tenants through impersonation only.
- **Permissions are cached per user and tenant** (`UserPermissionService.EntryKey`), tagged with the user, so `InvalidatePermissionCacheAsync` clears all of a user's tenants. A deactivated member gets an empty set.

## Tokens / sessions

**Sign-in** `POST /api/v1/identity/token/issue` needs no tenant (`TenantSignInService`): the `tenant` header if the user is an active member there (otherwise 401, like a wrong password), else `DefaultTenantId`, else the only usable tenant, else `SignInResponse` with `tenantSelectionRequired`, the tenants and a `selectionToken` (Data Protection, 5 minutes, carries the security stamp). `POST /token/select` finishes it, `POST /token/switch` (audited, refused while impersonating) moves a signed-in user, `GET /profile/tenants` feeds the menu; both take `setDefault`. Tokens are issued in the chosen tenant's scope (`IEventTenantScope` plus a new DI scope), so claims, session and audit records belong to it. The header `X-Trislab-App` enforces the app boundary after the tenant is chosen: a root account gets no token for `dashboard`, any other for `admin` (multi-tenant mode only). **The refresh token lives in the session** (`UserSession.RefreshTokenHash`, a SHA-256 of the token), one per sign-in, so `POST /token/refresh` needs no tenant and keeps the session's tenant; a missing, expired or revoked session is a 401. Forgot and reset password take no tenant either; self-register still needs the `tenant` header in multi-tenant mode. Admin can't demote/deactivate the last admin or the root-tenant seed admin (guards in `UserRoleService`/`UserStatusService`); deactivation changes the membership, never the account.

## Tests

`Identity.Tests` is the largest unit suite. When asserting a forwarded `CancellationToken`, assert the specific token (see `testing.md`).
