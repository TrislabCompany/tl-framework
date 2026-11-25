using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Eventing.Outbox;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.Events;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Localization;
using Microsoft.AspNetCore.DataProtection;
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

/// <summary>
/// Sign-in for global users: picks the tenant, issues tokens for it, refreshes them and switches tenants.
/// </summary>
/// <remarks>
/// <para>A user account is global and has a membership per tenant. Sign-in needs no tenant: it opens the
/// tenant the <c>tenant</c> header names when the user is a member there, else the user's default tenant,
/// else their only tenant. A user with several tenants and no default gets the list and a selection token,
/// valid for <see cref="SelectionTokenLifetime"/>, to send with the choice. A member of the root tenant (an
/// operator) always signs in to root.</para>
/// <para>Tokens are issued in the chosen tenant's own scope, so the claims, the session (which holds the
/// refresh token) and the audit records all belong to that tenant whatever the request's tenant was.</para>
/// </remarks>
public sealed class TenantSignInService(
    IdentityDbContext db,
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IEventTenantScope tenantScope,
    IServiceScopeFactory scopeFactory,
    IDataProtectionProvider dataProtection,
    ISecurityAudit securityAudit,
    IOptions<TenancyOptions> tenancy,
    IOptions<TenantGraceOptions> grace,
    TimeProvider timeProvider,
    ILogger<TenantSignInService> logger)
{
    /// <summary>How long a sign-in's selection token is valid.</summary>
    public static readonly TimeSpan SelectionTokenLifetime = TimeSpan.FromMinutes(5);

    private const string SelectionPurpose = "Trislab.Identity.TenantSelection.v1";

    private bool IsMultitenant => tenancy.Value.Mode == TenancyMode.Multitenant;

    /// <summary>Signs a user in whose credentials are checked: opens a tenant or answers with the list.</summary>
    public async Task<SignInResponse> SignInAsync(
        string userId, string? requestedTenant, string? app, SignInCaller caller, CancellationToken ct)
    {
        var account = await FindAccountAsync(userId, ct).ConfigureAwait(false)
            ?? throw new UnauthorizedException();
        var memberships = await GetActiveMembershipsAsync(userId, ct).ConfigureAwait(false);

        string tenantId;
        if (memberships.Contains(MultitenancyConstants.Root.Id))
        {
            // Operators sign in to root only; they reach other tenants through impersonation.
            tenantId = MultitenancyConstants.Root.Id;
        }
        else if (IsMultitenant && !string.IsNullOrWhiteSpace(requestedTenant))
        {
            // The same 401 as a wrong password: never tell a caller which tenants an account belongs to.
            tenantId = memberships.FirstOrDefault(m => string.Equals(m, requestedTenant.Trim(), StringComparison.Ordinal))
                ?? throw new UnauthorizedException();
        }
        else
        {
            var usable = await GetUsableTenantsAsync(memberships, ct).ConfigureAwait(false);
            if (usable.Count == 0)
            {
                if (memberships.Count == 0)
                {
                    throw new UnauthorizedException();
                }

                // Every tenant of the user is deactivated or expired: say why, as sign-in to that tenant did.
                ValidateTenantStatus(await FindTenantAsync(memberships[0], ct).ConfigureAwait(false), memberships[0]);
                throw new UnauthorizedException();
            }

            var preferred = usable.FirstOrDefault(t => string.Equals(t.Id, account.DefaultTenantId, StringComparison.Ordinal));
            if (preferred is null && usable.Count > 1)
            {
                EnsureAppBoundary(app, tenantId: null);
                return SignInResponse.ChooseTenant(
                    CreateSelectionToken(account.Id, account.SecurityStamp),
                    usable.Select(t => new TenantMembershipDto(t.Id, t.Name, IsDefault: false)).ToList());
            }

            tenantId = (preferred ?? usable[0]).Id;
        }

        return await OpenTenantAsync(userId, tenantId, app, TokenIssueKind.SignIn, caller, fromTenant: null, ct).ConfigureAwait(false);
    }

    /// <summary>Finishes a sign-in that answered with the tenant list.</summary>
    public async Task<SignInResponse> SelectAsync(
        string selectionToken, string tenantId, bool setDefault, string? app, SignInCaller caller, CancellationToken ct)
    {
        var userId = await ReadSelectionTokenAsync(selectionToken, ct).ConfigureAwait(false)
            ?? throw new UnauthorizedException("the selection token is invalid or expired")
                .Localized<IdentityResources>("Identity.SelectionTokenInvalid");

        var memberships = await GetActiveMembershipsAsync(userId, ct).ConfigureAwait(false);
        if (!memberships.Contains(tenantId, StringComparer.Ordinal))
        {
            throw new UnauthorizedException();
        }

        var response = await OpenTenantAsync(userId, tenantId, app, TokenIssueKind.SignIn, caller, fromTenant: null, ct).ConfigureAwait(false);

        // Only once the tenant opened: a refused sign-in must not change the default.
        if (setDefault)
        {
            await SetDefaultTenantAsync(userId, tenantId, ct).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>Moves a signed-in user to another of their tenants.</summary>
    public async Task<SignInResponse> SwitchAsync(
        string userId, string? currentTenant, string tenantId, bool setDefault, string? app, SignInCaller caller, CancellationToken ct)
    {
        var memberships = await GetActiveMembershipsAsync(userId, ct).ConfigureAwait(false);
        if (!memberships.Contains(tenantId, StringComparer.Ordinal))
        {
            throw new ForbiddenException("not a member of this tenant")
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
    /// refresh token, and the audit records. Throws a 401 when the user is not an active member there.
    /// </summary>
    public async Task<TokenResponse> IssueAsync(
        string userId, string tenantId, TokenIssueKind kind, SignInCaller caller, string? fromTenant, CancellationToken ct)
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
            var sessions = services.GetRequiredService<ISessionService>();
            var audit = services.GetRequiredService<ISecurityAudit>();

            issued = await tokens.IssueAsync(subject, claimList, tenantId, token).ConfigureAwait(false);

            // The session holds the refresh token: a user signed in to two tenants has two sessions.
            await sessions.CreateSessionAsync(
                subject,
                HashRefreshToken(issued.RefreshToken),
                caller.IpAddress,
                caller.UserAgent,
                issued.RefreshTokenExpiresAt,
                token).ConfigureAwait(false);

            if (kind == TokenIssueKind.SignIn)
            {
                await audit.LoginSucceededAsync(subject, userName, caller.ClientId, caller.IpAddress, caller.UserAgent, token).ConfigureAwait(false);
            }
            else if (kind == TokenIssueKind.TenantSwitch)
            {
                await services.GetRequiredService<IAuditClient>().WriteSecurityAsync(
                    SecurityAction.TokenIssued,
                    subjectId: subject,
                    clientId: caller.ClientId,
                    reasonCode: "TenantSwitched",
                    claims: new Dictionary<string, object?> { ["fromTenant"] = fromTenant, ["toTenant"] = tenantId },
                    severity: AuditSeverity.Information,
                    source: "Identity",
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
    /// Rotates a refresh token. The session that holds it names the user and the tenant, so the refresh
    /// needs no tenant header. The new tokens are for the same tenant.
    /// </summary>
    public async Task<TokenResponse> RefreshAsync(string refreshToken, string? accessToken, string clientId, CancellationToken ct)
    {
        var hash = HashRefreshToken(refreshToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Sessions are tenant-scoped and the caller has no tenant yet: find the session across tenants.
        var session = await db.UserSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.RefreshTokenHash == hash)
            .Select(s => new { s.UserId, s.IsRevoked, s.ExpiresAt, TenantId = EF.Property<string>(s, "TenantId") })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (session is null || session.ExpiresAt <= now)
        {
            logger.LogWarning("Refresh token not found or expired");
            await securityAudit.TokenRevokedAsync(session?.UserId ?? "unknown", clientId, "InvalidRefreshToken", ct).ConfigureAwait(false);
            throw new UnauthorizedException("refresh token is invalid or expired")
                .Localized<IdentityResources>("Identity.RefreshTokenInvalidOrExpired");
        }

        if (session.IsRevoked)
        {
            await securityAudit.TokenRevokedAsync(session.UserId, clientId, "SessionRevoked", ct).ConfigureAwait(false);
            throw new UnauthorizedException("Session has been revoked.")
                .Localized<IdentityResources>("Identity.SessionRevoked");
        }

        EnsureAccessTokenSubject(accessToken, session.UserId);

        ValidateTenantStatus(await FindTenantAsync(session.TenantId, ct).ConfigureAwait(false), session.TenantId);

        TokenResponse? issued = null;
        await tenantScope.ExecuteAsync(session.TenantId, async token =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            var built = await IdentityService.BuildClaimsInScopeAsync(services, session.UserId, session.TenantId, token).ConfigureAwait(false)
                ?? throw new UnauthorizedException("user not found").Localized<IdentityResources>("Identity.UserNotFound");
            var (subject, claims) = built;
            var claimList = claims.ToList();

            var audit = services.GetRequiredService<ISecurityAudit>();
            await audit.TokenRevokedAsync(subject, clientId, "RefreshTokenRotated", token).ConfigureAwait(false);

            var next = await services.GetRequiredService<ITokenService>().IssueAsync(subject, claimList, session.TenantId, token).ConfigureAwait(false);

            // Rotate atomically: only the request that still finds the old hash on a live session wins. A
            // concurrent refresh with the same token, or a revoke in between, gets a 401 and no tokens.
            var rotatedAt = timeProvider.GetUtcNow().UtcDateTime;
            var rotated = await services.GetRequiredService<IdentityDbContext>().UserSessions
                .Where(s => s.RefreshTokenHash == hash && !s.IsRevoked)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.RefreshTokenHash, HashRefreshToken(next.RefreshToken))
                    .SetProperty(s => s.ExpiresAt, next.RefreshTokenExpiresAt)
                    .SetProperty(s => s.LastActivityAt, rotatedAt), token)
                .ConfigureAwait(false);
            if (rotated == 0)
            {
                await audit.TokenRevokedAsync(subject, clientId, "RefreshTokenReused", token).ConfigureAwait(false);
                throw new UnauthorizedException("refresh token is invalid or expired")
                    .Localized<IdentityResources>("Identity.RefreshTokenInvalidOrExpired");
            }

            issued = next;

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
        EnsureAppBoundary(app, tenantId);
        ValidateTenantStatus(await FindTenantAsync(tenantId, ct).ConfigureAwait(false), tenantId);

        var token = await IssueAsync(userId, tenantId, kind, caller, fromTenant, ct).ConfigureAwait(false);
        return SignInResponse.Issued(token, tenantId);
    }

    private void EnsureAppBoundary(string? app, string? tenantId)
    {
        if (!IsMultitenant || string.IsNullOrWhiteSpace(app))
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
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => m.TenantId)
            .ToListAsync(ct);

    private Task<AccountInfo?> FindAccountAsync(string userId, CancellationToken ct) =>
        db.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AccountInfo(u.Id, u.SecurityStamp, u.DefaultTenantId, u.EmailConfirmed))
            .FirstOrDefaultAsync(ct);

    private Task<int> SetDefaultTenantAsync(string userId, string tenantId, CancellationToken ct) =>
        db.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DefaultTenantId, tenantId), ct);

    private string CreateSelectionToken(string userId, string? securityStamp) =>
        SelectionProtector().Protect($"{userId}\n{securityStamp}", SelectionTokenLifetime);

    private async Task<string?> ReadSelectionTokenAsync(string selectionToken, CancellationToken ct)
    {
        string payload;
        try
        {
            payload = SelectionProtector().Unprotect(selectionToken, out _);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var parts = payload.Split('\n', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        // A password change or a reset rotates the security stamp, which cancels a pending choice.
        var account = await FindAccountAsync(parts[0], ct).ConfigureAwait(false);
        return account is not null
            && account.EmailConfirmed
            && string.Equals(account.SecurityStamp ?? string.Empty, parts[1], StringComparison.Ordinal)
            ? account.Id
            : null;
    }

    private ITimeLimitedDataProtector SelectionProtector() =>
        dataProtection.CreateProtector(SelectionPurpose).ToTimeLimitedDataProtector();

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

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 8));

    private sealed record AccountInfo(string Id, string? SecurityStamp, string? DefaultTenantId, bool EmailConfirmed);
}
