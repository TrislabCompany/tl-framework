---
paths:
  - "src/BuildingBlocks/Web/**"
  - "src/BuildingBlocks/Quota/**"
  - "src/Tests/TestHost/**"
---

# Web security & request governance

CORS, security headers, rate limiting, idempotency, quota enforcement. `src/BuildingBlocks/Web/` + `Quota/`.
For auth/JWT/permissions see `modules/identity.md`; for the global exception handler see `api-conventions.md`.

## CORS (`Web/Cors/`) — the SignalR gotcha

Policy `TrislabCorsPolicy`. When `CorsOptions.AllowAll=true` it uses **`SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()`** — deliberately **NOT `AllowAnyOrigin()`**. `Access-Control-Allow-Origin: *` is illegal with credentialed requests, and **SignalR's negotiate always runs credentialed**, so `AllowAnyOrigin()` silently breaks SignalR while REST keeps working. Never "simplify" it to `AllowAnyOrigin()`. `UseTrislabCors()` runs **before** `UseHttpsRedirection()` so OPTIONS preflight isn't 307-redirected.

## Front-end origin for outbound links (`Web/Frontend/`)

`IFrontendOriginResolver` builds the origin of user-facing links sent in e-mails (password reset, e-mail confirmation). Backed by **`FrontendOptions`**, deliberately separate from `CorsOptions`: CORS governs which browsers may *call* the API, this governs which origins may appear *inside an outbound link*. Never merge the two lists — coupling them breaks same-origin/reverse-proxy topologies and overloads a security boundary.

- **`ResolveForCurrentRequest()`** — self-service flows only (forgot-password, self-register), where the caller *is* the recipient. Validates the request `Origin` against `FrontendOptions:AllowedOrigins` and returns the **canonical configured entry**, never the client's string. Present-but-unlisted against a non-empty list → `CustomException(HttpStatusCode.BadRequest)`: these endpoints are anonymous, so a forged header must never reach an e-mail. No `Origin` header, or an empty/all-unparseable list, falls through to the default.
- **`ResolveDefault()`** — operator-driven flows (admin register, resend-confirmation) and background jobs, where the caller is **not** the recipient. Chain: `FrontendOptions:DefaultOrigin` → throw. Never the caller's `Origin`, or an operator would send a tenant user a link into the admin console; and never the request's `Host` either, which is the same string under a different name. There is deliberately no fallback to `OriginOptions:OriginUrl`: these links address SPA routes (`/confirm-email`, `/reset-password`), so the API's own origin would 404 them.

Picking the wrong method is a silent bug — both compile and both return a plausible origin. Match the method to **who receives the link**, not to who sent the request.

No `ValidateOnStart` on `FrontendOptions`, on purpose: a deployment that never sends such a link must not be taken down by the setting. `UseTrislabPlatform` logs at startup instead — `Warning` for an empty or all-typo allow-list (counted after normalization, so a list of nothing but typos reports as the empty list it effectively is), `Error` for a missing `DefaultOrigin`, since without it those flows return 500 rather than degrading. `OriginOptions:OriginUrl` keeps its own job — the API's own public base for back-end-served assets (avatars), read through `IRequestContext.Origin`.

## Security headers (`Web/Security/`)

`UseTrislabSecurityHeaders()` sets `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, HSTS (HTTPS), and a CSP. `SecurityHeadersOptions.ExcludedPaths` defaults to `["/scalar","/openapi"]` (they manage their own scripts) — keep those excluded.

## Health endpoints (`Web/Health/`)

The probes `GET /api/health/live` and `/ready` are anonymous, so they answer the overall status only, never which check failed or its data. `GET /api/health/details` runs every check with its details and needs `Health.View` (Admin, and the operator). A check whose outage must not take the instance out of service is registered `Degraded` with the tag `HealthCheckTags.DetailsOnly` (`Shared/Health`): only `/details` runs it. File storage is one: `AddTrislabStorage` registers `storage`, which lists one key of the S3 bucket (the credentials need `s3:ListBucket` on the prefix) or writes and deletes a file in the local folder. The storage service itself turns every error into "not found", so never build a health check on it. A host that maps its own probes (`/readyz`) leaves the details-only checks out with a predicate, as `src/Tests/TestHost/Program.cs` does.

## Rate limiting (`Web/RateLimiting/`)

Chained partitioned fixed-window limiter: **tenant → user → IP** (defaults 1000 / 200 / 300 per 60s) + a stricter named `"auth"` policy (10/60s). Health paths are unlimited. Rejection → 429 + ProblemDetails + `Retry-After`. `RateLimitingOptions.Enabled` is read **eagerly** — when false the middleware is skipped entirely (tests set it via env var before host build).

## Idempotency (`Web/Idempotency/`)

Opt-in per endpoint with **`.WithIdempotency()`** on **authenticated** POST/PUTs that must be replay-safe (e.g. CreateTenant). Reads `Idempotency-Key` (max 128 chars, `DefaultTtl` 24h); a replay returns the cached status + body + `Location`/`ETag` and `Idempotency-Replayed: true`.

- **Key scope:** resolved tenant (never the raw `tenant` header) + operation (method + route pattern) + route values + caller (`GetUserId()`, else `"anon"`), via `CacheKeys.IdempotencyEntry`.
- **Only 2xx is stored**, before the body is sent, on `CancellationToken.None`. Probe and write must share one `IDistributedCache` + key + serializer, or replay silently never engages.
- **Concurrent duplicates:** in-flight reservation under a `lock:` prefix (Redis `SET NX`, else in-process, `ReservationTtl` default 1m); cache re-probed after the lock; duplicate in flight → **409** + `Retry-After: 1`. Probe, reserve and release all fail open.
- **`RequestAborted` is detached** while the handler runs, so a client disconnect can't cancel a committed side effect. Keep these handlers short; **never** on streaming or large-file endpoints (the response is buffered).
- **Never on `AllowAnonymous()`** endpoints — all anonymous callers share `"anon"`. `IdempotencyWiringTests` fails the build if one appears.

## Bot protection (`Web/BotProtection/`)

- A public form (an anonymous endpoint that creates something: a contact form, a sign-up, a lead) ends with `.RequireTurnstile()`. The filter refuses a request without a valid token in `X-Turnstile-Token` before the handler runs: 400 without a token, 403 when Cloudflare refuses it, 503 when the check can't be made.
- It **fails closed**: with no `TurnstileOptions:SecretKey` every request is refused. Never add a switch that lets requests through; locally and in tests use Cloudflare's test secrets (`TurnstileOptions.TestSecretThatPasses` / `TestSecretThatFails`).
- The front end renders `Turnstile` from `@trislabcompany/framework-ui` with the `PUBLIC_TURNSTILE_SITE_KEY` setting. Details: `docs/bot-protection.md`.

## Quota enforcement (`Quota/`)

`QuotaEnforcementMiddleware` charges 1 `ApiCalls` unit per request via `CheckAndRecordAsync`; over-limit → 429 + ProblemDetails + `Retry-After`, and sets `HttpContext.Items[QuotaRejected]` so auditing can tag it. Resources: `ApiCalls` (counter), `StorageBytes`, `Users`, `ActiveFeatureFlags` (gauges). Skips health/metrics, unresolved tenants, and the root tenant. **Pipeline:** runs after auth (needs tenant) and after the rate limiter. Inject `TimeProvider` (not `DateTimeOffset.UtcNow`) for any time math here — the subsystem is `TimeProvider`-based.

`IQuotaService`: `CheckAsync` (no mutation), `RecordAsync` (increment), `CheckAndRecordAsync` (atomic — won't increment past the limit). Store: Redis (`RedisQuotaService`) or per-process `InMemoryQuotaService` (dev/test). `NoopQuotaService` when disabled.

## Identity: accounts, invitations and sessions (`Modules/Identity`)

The rules every change to sign-in, users or sessions keeps (details in `modules/identity.md`):

- **Nobody but the owner sets a password or confirms an e-mail.** Users join a tenant by invitation; the owner accepts the single-use, hashed, expiring, revocable link. No endpoint lets an administrator set another user's password or confirm another user's e-mail.
- **No account enumeration.** Sign-in, self-registration, forgot-password and reset answer the same, and take the same least time, whether or not an address has an account; a lockout is shown only to a caller with the right password.
- **Re-check after the password.** Every token the server issues after sign-in (selection, refresh, switch) re-checks the account, the stamp, the membership, the tenant and the app boundary.
- **Tokens:** selection tokens work once; refresh tokens rotate atomically and their reuse ends the session; an access token names its session (`sess`), and a tenant switch needs that session live; a password change or reset ends every session of the account, in one transaction with the new password.
- **Confirming an e-mail needs the code and the password** (`POST /confirm-email`): the code proves the mailbox, the password proves who signed up, so registering someone else's address never yields an account its owner confirms by opening the link.
- **Invitations:** the answer and its least duration are the same for a new and an existing address; an account created for an invitation that is revoked unclaimed is deleted with it.
- **E-mailed links never sit in clear in the job store:** every e-mail is queued through `IBackgroundMailer`, which encrypts it with Data Protection (jobs.md). Audit records name a failed sign-in by a keyed hash of the address (`AuditSubjectHasher`, an HMAC with a key derived from the JWT signing key), never the address.
- **Audit every security event** with `IdentityAuditor` (`SecurityAction`), in the tenant the event is about. Never put a password, a token or an e-mail body in an audit record or a log.
- **Rate-limit** every anonymous Identity endpoint, and sending an invitation (it sends an e-mail), with the `auth` policy. `AuthRateLimitWiringTests` finds every anonymous Identity endpoint by its metadata, so a new one can't be missed; listing and revoking invitations are Admin calls under the global limiter.
