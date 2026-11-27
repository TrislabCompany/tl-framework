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
- **Adding a user is an invitation, never a password.** `MembershipInvitationService` creates a pending membership (`TenantMembership.CreatePending`) and an invitation whose token is only stored as a SHA-256; the owner accepts (`AcceptAsync`): a new or unconfirmed account (`NeedsPassword`) sets its password there, which confirms its e-mail and drops the memberships it joined on its own before (a sign-up with someone else's address); a confirmed account with a password must be the signed-in caller. The accept is one transaction (token, password, membership). Never add an endpoint that sets another user's password or confirms another user's e-mail (the old `POST /register` and admin-confirm-email are gone). A pending member is invisible: `MembershipFilter` shows only accepted memberships, and `Activate` throws on a pending one. `CreateAccepted` is only for self-registration and the platform's seeding of a tenant's first admin (a new account; an existing one is invited).
- **Self-registration and the password reset must not enumerate accounts:** same body, same least duration (`ResponseTime.PadAsync`), and an e-mail to the existing owner instead of an error. Sign-in verifies a dummy hash for an unknown address, and a locked account answers 401 unless the password is right.
- **Root members are operators** and have no other membership: `UserRegistrationService.AddMembershipAsync` and the seeding refuse it both ways. Operators only read inside other tenants: through the `tenant` header (Multitenancy) and through impersonation.
- **Impersonation only reads and needs the user's approval (D-44).** `ImpersonationRequest` (global, like `ImpersonationGrant`) is pending → approved or denied by its target user only (anyone else gets 404) → used once by its actor's `StartImpersonation`, which consumes it with a conditional update; 24 hours to answer and start. Denying a used request revokes its grant. Every state change is a conditional `ExecuteUpdate` (approve only a pending one, deny whatever its state, start only an approved one), and start and deny each re-check the other afterwards, so a deny always ends the impersonation; a start that fails after taking the approval hands it back. One open request per operator and user (409 `Identity.ImpersonationRequestOpen`). `ImpersonationReadOnly` (module middleware) refuses every write of an `act_sub` token with 403 `Identity.ImpersonationReadOnly`, except the endpoints named `EndImpersonation` and `SseToken` and the realtime hub's transport; `ImpersonationHubFilter` refuses every hub method but `JoinChannel` (refuse by default, so a new hub method is closed too). Keep an allowed write out of tenant data.
- **Restore admin access** (`RestoreAdminAccessCommandHandler`, root-only permission `Users.RestoreAdmin`) runs in the target tenant's scope (`IEventTenantScope` plus a new DI scope) and changes only the Admin role of one address: an active member gets it, or an invitation with `GrantsAdmin` gives it on accept. It e-mails the tenant's active Admins and audits `AdminAccessRestored` in the tenant.
- **Permissions are cached per user and tenant** (`UserPermissionService.EntryKey`), tagged with the user, so `InvalidatePermissionCacheAsync` clears all of a user's tenants. A deactivated member gets an empty set.

## Tokens / sessions

**Sign-in** `POST /api/v1/identity/token/issue` needs no tenant (`TenantSignInService`): the `tenant` header if the user is an active member there (otherwise 401, like a wrong password), else `DefaultTenantId`, else the only usable tenant, else `SignInResponse` with `tenantSelectionRequired`, the tenants and a `selectionToken` (Data Protection, 5 minutes, carries the security stamp). `POST /token/select` finishes it, `POST /token/switch` (audited, refused while impersonating) moves a signed-in user: the access token's `sess` claim names its session, which must still be live, in the token's tenant and on the current stamp, so a token of a signed-out or revoked session can't open a new one; `GET /profile/tenants` feeds the menu; both take `setDefault`. Tokens are issued in the chosen tenant's scope (`IEventTenantScope` plus a new DI scope), so claims, session and audit records belong to it. The header `X-Trislab-App` enforces the app boundary after the tenant is chosen: a root account gets no token for `dashboard`, any other for `admin` (multi-tenant mode only). **Re-checks:** the selection (its token's row in `SignInSelections` is deleted on use, so it works once, and it carries the stamp hash), every refresh and every switch re-check the account (confirmed, not locked out, stamp: the `sst` claim and `UserSession.SecurityStampHash`), the membership, the tenant and the app boundary (`TenantSignInService`). Refresh rotates with a conditional update and keeps `PreviousRefreshTokenHash`: an old token coming back revokes the session and is audited as `RefreshTokenReuseDetected`. Password change and reset revoke every session of the account (`SessionRevocation`). Write security events with `IdentityAuditor` inside the tenant they are about; an event about the account itself (a password reset or change, a lockout) goes through `WriteForAccountAsync`, which writes it in every tenant where the account is an accepted member, never in a tenant the caller merely names, and logs a failed write instead of changing the answer. **The refresh token lives in the session** (`UserSession.RefreshTokenHash`, a SHA-256 of the token), one per sign-in, so `POST /token/refresh` needs no tenant and keeps the session's tenant; a missing, expired or revoked session is a 401. Forgot and reset password take no tenant either. The reset checks the token first, then the password history (which includes the current password), and only then changes anything, in one transaction: new password, e-mail confirmed, lockout ended, history row, every session revoked. Account-level lookups use `IdentityDbContext.FindAccountAsync`, never `UserManager.FindByIdAsync`, which only finds members of the current tenant; self-register still needs the `tenant` header in multi-tenant mode. Admin can't demote/deactivate the last admin or the root-tenant seed admin (guards in `UserRoleService`/`UserStatusService`); deactivation changes the membership, never the account.

## Tests

`Identity.Tests` is the largest unit suite. When asserting a forwarded `CancellationToken`, assert the specific token (see `testing.md`).
