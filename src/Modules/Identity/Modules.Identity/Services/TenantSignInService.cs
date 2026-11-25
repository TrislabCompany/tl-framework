using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Eventing.Outbox;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.Events;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace Trislab.Modules.Identity.Services;

/// <summary>Why tokens are issued; decides which audit records and events go with them.</summary>
public enum TokenIssueKind
{
    /// <summary>A sign-in that opened a tenant directly, or a tenant chosen from the sign-in's list.</summary>
    SignIn,

    /// <summary>A signed-in user moved to another of their tenants.</summary>
    TenantSwitch,

    /// <summary>An operator ended an impersonation and gets their own tokens back.</summary>
    ImpersonationEnded,
}

/// <summary>Who asked, for the audit records of a sign-in.</summary>
public sealed record SignInCaller(string IpAddress, string UserAgent, string ClientId, string? Email = null);

/// <summary>What sign-in does with a user's tenants.</summary>
public enum TenantDecisionKind
{
    /// <summary>Open <see cref="TenantDecision.TenantId"/>.</summary>
    Open,

    /// <summary>Several usable tenants and no default: answer with the list.</summary>
    Choose,

    /// <summary>No tenant to open, or one the user is not a member of: the 401 of a wrong password.</summary>
    Refuse,

    /// <summary>The user is a member, but every tenant of theirs is deactivated or expired.</summary>
    NoUsableTenant,
}

public sealed record TenantDecision(TenantDecisionKind Kind, string? TenantId = null);

/// <summary>
/// Sign-in for global users: picks the tenant, issues tokens for it, refreshes them and switches tenants.
/// </summary>
/// <remarks>
/// <para>A user account is global and has a membership per tenant. Sign-in needs no tenant: it opens the
/// tenant the <c>tenant</c> header names when the user is a member there, else the user's default tenant,
/// else their only tenant. A user with several tenants and no default gets the list and a single-use
/// selection token. A member of the root tenant (an operator) always signs in to root.</para>
/// <para>Every step after the password re-checks the account (confirmed e-mail, not locked out, security
/// stamp unchanged), the membership (active), the tenant (active, not expired) and the app boundary: the
/// selection, every refresh and every switch. A refresh token is rotated atomically, and the reuse of an
/// old one ends its session (the token family).</para>
/// </remarks>
public sealed class TenantSignInService(
    IdentityDbContext db,
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IEventTenantScope tenantScope,
    IServiceScopeFactory scopeFactory,
    SelectionTokenCodec selectionTokens,
    ISecurityAudit securityAudit,
    IOptions<TenancyOptions> tenancy,
    IOptions<TenantGraceOptions> grace,
    TimeProvider timeProvider,
    ILogger<TenantSignInService> logger)
{
    private bool IsMultitenant => tenancy.Value.Mode == TenancyMode.Multitenant;

    /// <summary>
    /// Chooses what a sign-in opens: root for an operator; the requested tenant if the user is a member there;
    /// else the default; else the only usable tenant; else the list.
    /// </summary>
    internal static TenantDecision ChooseTenant(
        IReadOnlyCollection<string> memberships,
        IReadOnlyList<string> usableTenants,
        string? defaultTenant,
        string? requestedTenant,
        bool multitenant)
    {
        if (memberships.Contains(MultitenancyConstants.Root.Id))
        {
            return new(TenantDecisionKind.Open, MultitenancyConstants.Root.Id);
        }

        if (multitenant && !string.IsNullOrWhiteSpace(requestedTenant))
        {
            // The same 401 as a wrong password: never tell a caller which tenants an account belongs to.
            var requested = requestedTenant.Trim();
            return memberships.Contains(requested, StringComparer.Ordinal)
                ? new(TenantDecisionKind.Open, requested)
                : new(TenantDecisionKind.Refuse);
        }

        if (usableTenants.Count == 0)
        {
            return memberships.Count == 0 ? new(TenantDecisionKind.Refuse) : new(TenantDecisionKind.NoUsableTenant);
        }

        var preferred = usableTenants.FirstOrDefault(t => string.Equals(t, defaultTenant, StringComparison.Ordinal));
        if (preferred is null && usableTenants.Count > 1)
        {
            return new(TenantDecisionKind.Choose);
        }

        return new(TenantDecisionKind.Open, preferred ?? usableTenants[0]);
    }

    /// <summary>
    /// The app boundary: root accounts get tokens for the admin app only, tenant accounts never for it.
    /// No app named, or single-tenant mode: no check.
    /// </summary>
    internal static void EnsureAppBoundary(string? app, string? tenantId, bool multitenant)
    {
        if (!multitenant || string.IsNullOrWhiteSpace(app))
        {
            return;
        }

        var isRoot = string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal);
        if (isRoot && string.Equals(app, GenerateTokenEndpoint.AppDashboard, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("operator accounts sign in to the admin app only")
                .Localized("Error.AppBoundary.Detail");
        }

        if (!isRoot && string.Equals(app, GenerateTokenEndpoint.AppAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("the admin app is for operators only")
                .Localized<IdentityResources>("Identity.OperatorAppBoundary.Detail");
        }
    }

    /// <summary>Signs a user in whose credentials are checked: opens a tenant or answers with the list.</summary>
    public async Task<SignInResponse> SignInAsync(
        string userId, string? requestedTenant, string? app, SignInCaller caller, CancellationToken ct)
    {
        var account = await RequireUsableAccountAsync(userId, ct).ConfigureAwait(false);
        var memberships = await GetActiveMembershipsAsync(userId, ct).ConfigureAwait(false);
        var usable = await GetUsableTenantsAsync(memberships, ct).ConfigureAwait(false);

        var decision = ChooseTenant(memberships, usable.Select(t => t.Id).ToList(), account.DefaultTenantId, requestedTenant, IsMultitenant);
        switch (decision.Kind)
        {
            case TenantDecisionKind.Refuse:
                throw new UnauthorizedException();
            case TenantDecisionKind.NoUsableTenant:
                // Every tenant of the user is deactivated or expired: say why, as a sign-in to that tenant would.
                ValidateTenantStatus(await FindTenantAsync(memberships[0], ct).ConfigureAwait(false), memberships[0]);
                throw new UnauthorizedException();
            case TenantDecisionKind.Choose:
                EnsureAppBoundary(app, tenantId: null, IsMultitenant);
                return SignInResponse.ChooseTenant(
                    await IssueSelectionTokenAsync(account, ct).ConfigureAwait(false),
                    usable.Select(t => new TenantMembershipDto(t.Id, t.Name, IsDefault: false)).ToList());
            default:
                return await OpenTenantAsync(userId, decision.TenantId!, app, TokenIssueKind.SignIn, caller, fromTenant: null, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Finishes a sign-in that answered with the tenant list. The selection token works once.</summary>
    public async Task<SignInResponse> SelectAsync(
        string selectionToken, string tenantId, bool setDefault, string? app, SignInCaller caller, CancellationToken ct)
    {
        var payload = selectionTokens.Read(selectionToken) ?? throw InvalidSelection();

        // Consume first, atomically: a second use finds no row, whatever happens after.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var consumed = await db.SignInSelections
            .Where(s => s.Id == payload.Id && s.UserId == payload.UserId && s.ExpiresOnUtc > now)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
        if (consumed == 0)
        {
            throw InvalidSelection();
        }

        // A password change or a reset rotates the security stamp, which cancels a pending choice.
        var account = await RequireUsableAccountAsync(payload.UserId, ct).ConfigureAwait(false);
        if (!string.Equals(IdentityService.StampHash(account.SecurityStamp), payload.StampHash, StringComparison.Ordinal))
        {
            throw InvalidSelection();
        }

        var memberships = await GetActiveMembershipsAsync(account.Id, ct).ConfigureAwait(false);
        if (!memberships.Contains(tenantId, StringComparer.Ordinal))
        {
            throw new UnauthorizedException();
        }

        var response = await OpenTenantAsync(account.Id, tenantId, app, TokenIssueKind.SignIn, caller, fromTenant: null, ct).ConfigureAwait(false);

        // Only once the tenant opened: a refused sign-in must not change the default.
        if (setDefault)
        {
            await SetDefaultTenantAsync(account.Id, tenantId, ct).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Moves a signed-in user to another of their tenants. <paramref name="tokenStampHash"/> is the access token's
    /// stamp claim: a token from before a password or 2FA change can't switch. <paramref name="tokenSessionId"/> is
    /// its session claim: the session must still be live, so a token whose session was signed out, revoked or
    /// ended by a removed membership can't open a new one.
    /// </summary>
    public async Task<SignInResponse> SwitchAsync(
        string userId,
        string? currentTenant,
        string? tokenStampHash,
        string? tokenSessionId,
        string tenantId,
        bool setDefault,
        string? app,
        SignInCaller caller,
        CancellationToken ct)
    {
        var account = await RequireUsableAccountAsync(userId, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(tokenStampHash)
            || !string.Equals(IdentityService.StampHash(account.SecurityStamp), tokenStampHash, StringComparison.Ordinal))
        {
            throw SessionEnded();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = Guid.TryParse(tokenSessionId, out var sessionId)
            ? await FindSessionAsync(s => s.Id == sessionId, ct).ConfigureAwait(false)
            : null;
        if (session is null
            || !string.Equals(session.UserId, userId, StringComparison.Ordinal)
            || session.IsRevoked
            || session.ExpiresAt <= now
            || !string.Equals(session.TenantId, currentTenant, StringComparison.Ordinal)
            || !string.Equals(session.SecurityStampHash, tokenStampHash, StringComparison.Ordinal))
        {
            throw SessionEnded();
        }

        var memberships = await GetActiveMembershipsAsync(userId, ct).ConfigureAwait(false);
        if (!memberships.Contains(tenantId, StringComparer.Ordinal))
        {
            throw new ForbiddenException("not a member of this tenant")
                .Localized<IdentityResources>("Identity.NotATenantMember");
        }

        // Operators stay in root; a member of root has no other membership to switch to (checked anyway).
        if (memberships.Contains(MultitenancyConstants.Root.Id) && !string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            throw new ForbiddenException("operators reach other tenants through impersonation")
                .Localized<IdentityResources>("Identity.NotATenantMember");
        }

        var response = await OpenTenantAsync(userId, tenantId, app, TokenIssueKind.TenantSwitch, caller, currentTenant, ct).ConfigureAwait(false);

        if (setDefault)
        {
            await SetDefaultTenantAsync(userId, tenantId, ct).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>The tenants a user can open now: active memberships of active, unexpired tenants.</summary>
    public async Task<List<TenantMembershipDto>> GetMyTenantsAsync(string userId, CancellationToken ct)
    {
        var account = await FindAccountAsync(userId, ct).ConfigureAwait(false);
        if (account is null)
        {
            return [];
        }

        var memberships = await GetActiveMembershipsAsync(userId, ct).ConfigureAwait(false);
        var usable = await GetUsableTenantsAsync(memberships, ct).ConfigureAwait(false);
        return usable
            .Select(t => new TenantMembershipDto(t.Id, t.Name, string.Equals(t.Id, account.DefaultTenantId, StringComparison.Ordinal)))
            .ToList();
    }

    /// <summary>
    /// Issues tokens for a member in a tenant, in that tenant's scope: claims, a new session that holds the
    /// refresh token (bound to the account's security stamp and the app), and the audit records. Throws a 401
    /// when the user is not an active member there.
    /// </summary>
    public async Task<TokenResponse> IssueAsync(
        string userId, string tenantId, TokenIssueKind kind, SignInCaller caller, string? fromTenant, CancellationToken ct, string? app = null)
    {
        TokenResponse? issued = null;
        await tenantScope.ExecuteAsync(tenantId, async token =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            var built = await IdentityService.BuildClaimsInScopeAsync(services, userId, tenantId, token).ConfigureAwait(false)
                ?? throw new UnauthorizedException();
            var (subject, claims) = built;
            var claimList = claims.ToList();
            var userName = claimList.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? caller.Email ?? string.Empty;

            var tokens = services.GetRequiredService<ITokenService>();
            var audit = services.GetRequiredService<ISecurityAudit>();
            var scopedDb = services.GetRequiredService<IdentityDbContext>();

            // The access token names its session, so a revoked session can't be used to open another.
            var sessionId = Guid.NewGuid();
            claimList.Add(new Claim(IdentityService.SessionClaim, sessionId.ToString()));
            issued = await tokens.IssueAsync(subject, claimList, tenantId, token).ConfigureAwait(false);

            // The session holds the refresh token: a user signed in to two tenants has two sessions.
            var session = SessionService.NewSession(
                subject, HashRefreshToken(issued.RefreshToken), caller.IpAddress, caller.UserAgent, issued.RefreshTokenExpiresAt, sessionId);
            session.Bind(claimList.FirstOrDefault(c => c.Type == IdentityService.SecurityStampClaim)?.Value, app);
            scopedDb.UserSessions.Add(session);
            await scopedDb.SaveChangesAsync(token).ConfigureAwait(false);

            if (kind == TokenIssueKind.SignIn)
            {
                await audit.LoginSucceededAsync(subject, userName, caller.ClientId, caller.IpAddress, caller.UserAgent, token).ConfigureAwait(false);
            }
            else if (kind == TokenIssueKind.TenantSwitch)
            {
                await services.GetRequiredService<IdentityAuditor>().WriteAsync(
                    SecurityAction.TenantSwitched,
                    subject,
                    "TenantSwitched",
                    new Dictionary<string, object?> { ["fromTenant"] = fromTenant, ["toTenant"] = tenantId, ["clientId"] = caller.ClientId },
                    ct: token).ConfigureAwait(false);
            }

            var fingerprint = Fingerprint(issued.AccessToken);
            await audit.TokenIssuedAsync(subject, userName, caller.ClientId, fingerprint, issued.AccessTokenExpiresAt, token).ConfigureAwait(false);

            if (kind == TokenIssueKind.SignIn)
            {
                await services.GetRequiredService<IOutboxStore>().AddAsync(new TokenGeneratedIntegrationEvent(
                    Id: Guid.NewGuid(),
                    OccurredOnUtc: timeProvider.GetUtcNow().UtcDateTime,
                    TenantId: tenantId,
                    CorrelationId: Guid.NewGuid().ToString(),
                    Source: "Identity",
                    UserId: subject,
                    Email: caller.Email ?? claimList.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? string.Empty,
                    ClientId: caller.ClientId,
                    IpAddress: caller.IpAddress,
                    UserAgent: caller.UserAgent,
                    TokenFingerprint: fingerprint,
                    AccessTokenExpiresAtUtc: issued.AccessTokenExpiresAt), token).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);

        return issued ?? throw new UnauthorizedException();
    }

    /// <summary>
    /// Rotates a refresh token. The session that holds it names the user and the tenant, so the refresh needs
    /// no tenant header. Re-checks the account, the stamp, the membership, the tenant and the app boundary.
    /// Presenting a token that was already rotated ends the session: a stolen token and its legitimate copy
    /// can't both stay in use.
    /// </summary>
    public async Task<TokenResponse> RefreshAsync(string refreshToken, string? accessToken, string? app, string clientId, CancellationToken ct)
    {
        var hash = HashRefreshToken(refreshToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Sessions are tenant-scoped and the caller has no tenant yet: find the session across tenants.
        var session = await FindSessionAsync(s => s.RefreshTokenHash == hash, ct).ConfigureAwait(false);
        if (session is null)
        {
            var reused = await FindSessionAsync(s => s.PreviousRefreshTokenHash == hash, ct).ConfigureAwait(false);
            if (reused is not null)
            {
                await EndSessionForReuseAsync(reused, clientId, ct).ConfigureAwait(false);
            }
            else
            {
                // Anonymous and unknown: log it, but don't let callers fill the audit trail with it.
                logger.LogInformation("Refresh with an unknown refresh token");
            }

            throw InvalidRefreshToken();
        }

        if (session.ExpiresAt <= now)
        {
            await securityAudit.TokenRevokedAsync(session.UserId, clientId, "RefreshTokenExpired", ct).ConfigureAwait(false);
            throw InvalidRefreshToken();
        }

        if (session.IsRevoked)
        {
            await securityAudit.TokenRevokedAsync(session.UserId, clientId, "SessionRevoked", ct).ConfigureAwait(false);
            throw SessionEnded();
        }

        EnsureAccessTokenSubject(accessToken, session.UserId);

        var account = await RequireUsableAccountAsync(session.UserId, ct).ConfigureAwait(false);
        if (!string.Equals(IdentityService.StampHash(account.SecurityStamp), session.SecurityStampHash, StringComparison.Ordinal))
        {
            // The password, the second factor or the stamp changed since this session signed in.
            await RevokeSessionAsync(session.Id, "SecurityStampChanged", ct).ConfigureAwait(false);
            await securityAudit.TokenRevokedAsync(session.UserId, clientId, "SecurityStampChanged", ct).ConfigureAwait(false);
            throw SessionEnded();
        }

        EnsureAppBoundary(app ?? session.App, session.TenantId, IsMultitenant);
        ValidateTenantStatus(await FindTenantAsync(session.TenantId, ct).ConfigureAwait(false), session.TenantId);

        TokenResponse? issued = null;
        await tenantScope.ExecuteAsync(session.TenantId, async token =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            // Null for a former member, a 401 for a deactivated or pending one.
            var built = await IdentityService.BuildClaimsInScopeAsync(services, session.UserId, session.TenantId, token).ConfigureAwait(false)
                ?? throw SessionEnded();
            var (subject, claims) = built;
            var claimList = claims.ToList();
            claimList.Add(new Claim(IdentityService.SessionClaim, session.Id.ToString()));

            var audit = services.GetRequiredService<ISecurityAudit>();
            var next = await services.GetRequiredService<ITokenService>().IssueAsync(subject, claimList, session.TenantId, token).ConfigureAwait(false);

            // Rotate atomically: only the request that still finds the old hash on a live session wins.
            var rotatedAt = timeProvider.GetUtcNow().UtcDateTime;
            var rotated = await services.GetRequiredService<IdentityDbContext>().UserSessions
                .Where(s => s.Id == session.Id && s.RefreshTokenHash == hash && !s.IsRevoked)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.PreviousRefreshTokenHash, hash)
                    .SetProperty(s => s.RefreshTokenHash, HashRefreshToken(next.RefreshToken))
                    .SetProperty(s => s.ExpiresAt, next.RefreshTokenExpiresAt)
                    .SetProperty(s => s.LastActivityAt, rotatedAt), token)
                .ConfigureAwait(false);
            if (rotated == 0)
            {
                // Another request rotated the same token first: treat it as reuse and end the family.
                await EndSessionForReuseAsync(session, clientId, token).ConfigureAwait(false);
                throw InvalidRefreshToken();
            }

            issued = next;
            await audit.TokenRevokedAsync(subject, clientId, "RefreshTokenRotated", token).ConfigureAwait(false);
            await audit.TokenIssuedAsync(
                subject,
                claimList.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty,
                clientId,
                Fingerprint(issued.AccessToken),
                issued.AccessTokenExpiresAt,
                token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return issued ?? throw new UnauthorizedException();
    }

    /// <summary>The refresh token's hash, as the session stores it. The raw token is never stored.</summary>
    internal static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private async Task<SignInResponse> OpenTenantAsync(
        string userId, string tenantId, string? app, TokenIssueKind kind, SignInCaller caller, string? fromTenant, CancellationToken ct)
    {
        EnsureAppBoundary(app, tenantId, IsMultitenant);
        ValidateTenantStatus(await FindTenantAsync(tenantId, ct).ConfigureAwait(false), tenantId);

        var token = await IssueAsync(userId, tenantId, kind, caller, fromTenant, ct, app).ConfigureAwait(false);
        return SignInResponse.Issued(token, tenantId);
    }

    private async Task<string> IssueSelectionTokenAsync(AccountInfo account, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Expired selections are only clutter; drop them while we are here.
        await db.SignInSelections.Where(s => s.ExpiresOnUtc < now).ExecuteDeleteAsync(ct).ConfigureAwait(false);

        var payload = new SelectionTokenPayload(
            Guid.NewGuid(), account.Id, IdentityService.StampHash(account.SecurityStamp), now.Add(SelectionTokenCodec.Lifetime));
        db.SignInSelections.Add(SignInSelection.Create(payload.Id, payload.UserId, payload.ExpiresOnUtc));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return selectionTokens.Protect(payload);
    }

    private async Task<AccountInfo> RequireUsableAccountAsync(string userId, CancellationToken ct)
    {
        var account = await FindAccountAsync(userId, ct).ConfigureAwait(false) ?? throw new UnauthorizedException();

        if (!account.EmailConfirmed)
        {
            throw new UnauthorizedException("email not confirmed")
                .Localized<IdentityResources>("Identity.EmailNotConfirmed");
        }

        if (account.LockoutEnabled && account.LockoutEnd is { } end && end > timeProvider.GetUtcNow())
        {
            throw new CustomException(
                "Account is temporarily locked due to too many failed login attempts. Try again later.",
                errors: null,
                HttpStatusCode.Locked)
                .Localized<IdentityResources>("Identity.AccountLocked");
        }

        return account;
    }

    private void ValidateTenantStatus(AppTenantInfo? tenant, string tenantId)
    {
        if (string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            return;
        }

        if (tenant is null || !tenant.IsActive)
        {
            throw new UnauthorizedException($"tenant {tenantId} is deactivated")
                .Localized<IdentityResources>("Identity.TenantDeactivated", tenantId);
        }

        // Honor the billing grace period: a lapsed tenant can still authenticate until
        // ValidUpto + grace (matching the request-time guard in MultitenancyModule).
        if (timeProvider.GetUtcNow().UtcDateTime > tenant.ValidUpto.AddDays(grace.Value.GracePeriodDays))
        {
            throw new UnauthorizedException($"tenant {tenantId} validity has expired")
                .Localized<IdentityResources>("Identity.TenantValidityExpired", tenantId);
        }
    }

    private bool IsUsable(AppTenantInfo? tenant, string tenantId)
    {
        if (string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            return true;
        }

        return tenant is { IsActive: true }
            && timeProvider.GetUtcNow().UtcDateTime <= tenant.ValidUpto.AddDays(grace.Value.GracePeriodDays);
    }

    private async Task<List<AppTenantInfo>> GetUsableTenantsAsync(List<string> memberships, CancellationToken ct)
    {
        var usable = new List<AppTenantInfo>(memberships.Count);
        foreach (var id in memberships)
        {
            var tenant = await FindTenantAsync(id, ct).ConfigureAwait(false);
            if (IsUsable(tenant, id))
            {
                usable.Add(tenant ?? new AppTenantInfo(id, id, id));
            }
        }

        return usable.OrderBy(t => t.Name ?? t.Id, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private async Task<AppTenantInfo?> FindTenantAsync(string tenantId, CancellationToken ct) =>
        await tenantStore.GetAsync(tenantId).WaitAsync(ct).ConfigureAwait(false);

    private Task<List<string>> GetActiveMembershipsAsync(string userId, CancellationToken ct) =>
        db.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive && m.AcceptedOnUtc != null)
            .Select(m => m.TenantId)
            .ToListAsync(ct);

    private Task<AccountInfo?> FindAccountAsync(string userId, CancellationToken ct) =>
        db.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AccountInfo(u.Id, u.SecurityStamp, u.DefaultTenantId, u.EmailConfirmed, u.LockoutEnabled, u.LockoutEnd))
            .FirstOrDefaultAsync(ct);

    private Task<SessionInfo?> FindSessionAsync(System.Linq.Expressions.Expression<Func<UserSession, bool>> match, CancellationToken ct) =>
        db.UserSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(match)
            .Select(s => new SessionInfo(
                s.Id, s.UserId, s.IsRevoked, s.ExpiresAt, EF.Property<string>(s, "TenantId"), s.SecurityStampHash, s.App))
            .FirstOrDefaultAsync(ct);

    private Task<int> RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return db.UserSessions
            .IgnoreQueryFilters()
            .Where(s => s.Id == sessionId && !s.IsRevoked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.IsRevoked, true)
                .SetProperty(s => s.RevokedAt, now)
                .SetProperty(s => s.RevokedBy, "system")
                .SetProperty(s => s.RevokedReason, reason), ct);
    }

    private async Task EndSessionForReuseAsync(SessionInfo session, string clientId, CancellationToken ct)
    {
        await RevokeSessionAsync(session.Id, "RefreshTokenReuse", ct).ConfigureAwait(false);
        logger.LogWarning("Refresh token reuse detected for session {SessionId}; the session is revoked", session.Id);

        await tenantScope.ExecuteAsync(session.TenantId, async token =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IdentityAuditor>().WriteAsync(
                SecurityAction.RefreshTokenReuseDetected,
                session.UserId,
                "RefreshTokenReuse",
                new Dictionary<string, object?> { ["sessionId"] = session.Id, ["clientId"] = clientId },
                AuditSeverity.Critical,
                token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private Task<int> SetDefaultTenantAsync(string userId, string tenantId, CancellationToken ct) =>
        db.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DefaultTenantId, tenantId), ct);

    private void EnsureAccessTokenSubject(string? accessToken, string subject)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        JwtSecurityToken? parsed = null;
        try
        {
            parsed = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        }
        catch (ArgumentException ex)
        {
            logger.LogDebug(ex, "Failed to parse access token during refresh; relying on refresh-token validation only");
        }

        var accessSubject = parsed?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? parsed?.Subject;
        if (!string.IsNullOrEmpty(accessSubject) && !string.Equals(accessSubject, subject, StringComparison.Ordinal))
        {
            throw new UnauthorizedException("Access token subject mismatch.")
                .Localized<IdentityResources>("Identity.AccessTokenSubjectMismatch");
        }
    }

    private static UnauthorizedException InvalidRefreshToken() =>
        (UnauthorizedException)new UnauthorizedException("refresh token is invalid or expired")
            .Localized<IdentityResources>("Identity.RefreshTokenInvalidOrExpired");

    private static UnauthorizedException SessionEnded() =>
        (UnauthorizedException)new UnauthorizedException("Session has been revoked.")
            .Localized<IdentityResources>("Identity.SessionRevoked");

    private static UnauthorizedException InvalidSelection() =>
        (UnauthorizedException)new UnauthorizedException("the selection token is invalid or expired")
            .Localized<IdentityResources>("Identity.SelectionTokenInvalid");

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 8));

    private sealed record AccountInfo(
        string Id, string? SecurityStamp, string? DefaultTenantId, bool EmailConfirmed, bool LockoutEnabled, DateTimeOffset? LockoutEnd);

    private sealed record SessionInfo(
        Guid Id, string UserId, bool IsRevoked, DateTime ExpiresAt, string TenantId, string? SecurityStampHash, string? App);
}
