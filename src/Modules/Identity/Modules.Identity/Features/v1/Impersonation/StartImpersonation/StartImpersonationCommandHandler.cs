using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Constants;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Impersonation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Microsoft.EntityFrameworkCore;
using Mediator;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.StartImpersonation;

public sealed class StartImpersonationCommandHandler
    : ICommandHandler<StartImpersonationCommand, ImpersonationResponse>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISecurityAudit _securityAudit;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly IImpersonationGrantService _grantService;
    private readonly TimeProvider _timeProvider;
    private readonly IdentityDbContext _db;
    private readonly ILogger<StartImpersonationCommandHandler> _logger;

    public StartImpersonationCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        ISecurityAudit securityAudit,
        ICurrentUser currentUser,
        IRequestContext requestContext,
        IImpersonationGrantService grantService,
        TimeProvider timeProvider,
        IdentityDbContext db,
        ILogger<StartImpersonationCommandHandler> logger)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _securityAudit = securityAudit;
        _currentUser = currentUser;
        _requestContext = requestContext;
        _grantService = grantService;
        _timeProvider = timeProvider;
        _db = db;
        _logger = logger;
    }

    public async ValueTask<ImpersonationResponse> Handle(
        StartImpersonationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var actorUserId = _currentUser.GetUserId().ToString();
        var actorTenantId = _currentUser.GetTenant()
            ?? throw new UnauthorizedException("missing tenant context").Localized("Error.InvalidTenant");
        var actorUserName = _currentUser.Name;

        // Prevent nesting: if the caller is already impersonating, require end-impersonation first.
        var callerClaims = _currentUser.GetUserClaims();
        if (callerClaims is not null
            && callerClaims.Any(c => c.Type == ClaimConstants.ActorSubject))
        {
            throw new CustomException(
                "end current impersonation before starting a new one",
                errors: null,
                System.Net.HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.EndImpersonationFirst");
        }

        // Only the caller's own request, approved by its user and not expired, starts an impersonation (D-44).
        // Another operator's request is a 404, so it can't be used or probed.
        var approval = await _db.ImpersonationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == request.RequestId && r.ActorUserId == actorUserId && r.ActorTenantId == actorTenantId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw ImpersonationRequests.NotFound();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Consume the approval atomically: of two concurrent starts, one wins, and a request starts once.
        var consumed = await _db.ImpersonationRequests
            .Where(r => r.Id == approval.Id && r.State == ImpersonationRequestState.Approved && r.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.State, ImpersonationRequestState.Used), cancellationToken)
            .ConfigureAwait(false);
        if (consumed == 0)
        {
            throw new ForbiddenException("the user has not approved this request")
                .Localized<IdentityResources>("Identity.ImpersonationNotApproved");
        }

        string targetTenantId, reason, subject, jti, accessToken;
        DateTime expiresAt;
        try
        {
            targetTenantId = approval.TargetTenantId;
            reason = approval.Reason;

            var targetClaimsResult = await _identityService
                .BuildClaimsForUserAsync(approval.TargetUserId, targetTenantId, cancellationToken);

            if (targetClaimsResult is null)
            {
                throw new NotFoundException("target user not found")
                    .Localized<IdentityResources>("Identity.TargetUserNotFound");
            }

            (subject, var claims) = targetClaimsResult.Value;
            var targetUserName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                ?? claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;

            // Strip the auto-generated jti from BuildClaimsForUserAsync and inject our own, so the persisted
            // ImpersonationGrant row and the issued JWT share the same jti.
            jti = Guid.NewGuid().ToString("N");
            var impersonationClaims = ImpersonationClaims(claims, jti, actorUserId, actorTenantId);

            // Cap the caller-supplied duration server-side (defense in depth: the validator already rejects
            // out-of-range, but a future caller bypassing it must not escape the cap).
            var lifetime = request.DurationMinutes is { } minutes
                ? TimeSpan.FromMinutes(Math.Clamp(minutes, 1, StartImpersonationCommandValidator.MaxImpersonationMinutes))
                : (TimeSpan?)null;

            var startedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            (accessToken, expiresAt) = await _tokenService.IssueAccessOnlyAsync(
                subject, impersonationClaims, lifetime, cancellationToken);

            // Persist the grant AFTER issuance so a failed issue leaves no orphan grant. CreateAsync primes the
            // cache so the JWT validation hook sees status=Active on the next request without a DB hit.
            var grant = await _grantService.CreateAsync(new CreateGrantInput(
                Jti: jti,
                ActorUserId: actorUserId,
                ActorUserName: actorUserName,
                ActorTenantId: actorTenantId,
                ImpersonatedUserId: subject,
                ImpersonatedUserName: targetUserName,
                ImpersonatedTenantId: targetTenantId,
                Reason: reason,
                StartedAtUtc: startedAtUtc,
                ExpiresAtUtc: expiresAt,
                ClientId: _requestContext.ClientId,
                IpAddress: _requestContext.IpAddress,
                UserAgent: _requestContext.UserAgent), cancellationToken);

            // The request names the impersonation it started, so the user can end it by taking the request back.
            await _db.ImpersonationRequests
                .Where(r => r.Id == approval.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.GrantId, grant.Id), cancellationToken)
                .ConfigureAwait(false);

            // A deny that landed while this start ran wins: the user said no, so the new grant is revoked.
            var state = await _db.ImpersonationRequests
                .AsNoTracking()
                .Where(r => r.Id == approval.Id)
                .Select(r => r.State)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            if (state == ImpersonationRequestState.Denied)
            {
                await _grantService.RevokeAsync(
                    grant.Id, approval.TargetUserId, approval.TargetUserName, "Taken back by the impersonated user",
                    CancellationToken.None).ConfigureAwait(false);
                throw new ForbiddenException("the user took the approval back")
                    .Localized<IdentityResources>("Identity.ImpersonationNotApproved");
            }
        }
        catch (Exception)
        {
            // Nothing started: hand the approval back, unless the user denied it meanwhile or a grant exists.
            await _db.ImpersonationRequests
                .Where(r => r.Id == approval.Id && r.State == ImpersonationRequestState.Used && r.GrantId == null)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.State, ImpersonationRequestState.Approved), CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }

        await _securityAudit.ImpersonationStartedAsync(
            actorUserId: actorUserId,
            actorTenantId: actorTenantId,
            targetUserId: subject,
            targetTenantId: targetTenantId,
            clientId: _requestContext.ClientId ?? "unknown",
            ip: _requestContext.IpAddress ?? "unknown",
            userAgent: _requestContext.UserAgent ?? "unknown",
            reason: reason,
            ct: cancellationToken);

        _logger.LogWarning(
            "Impersonation started: actor {ActorUserId}@{ActorTenant} -> target {TargetUserId}@{TargetTenant} jti={Jti}",
            actorUserId, actorTenantId, subject, targetTenantId, jti);

        return new ImpersonationResponse(
            AccessToken: accessToken,
            AccessTokenExpiresAt: expiresAt,
            ActorUserId: actorUserId,
            ActorTenantId: actorTenantId,
            ImpersonatedUserId: subject,
            ImpersonatedTenantId: targetTenantId);
    }

    /// <summary>
    /// The impersonation token's claims: the target's, without its jti and locale, plus the new jti and the
    /// RFC 8693 actor claims that record who is acting.
    /// </summary>
    public static IReadOnlyList<Claim> ImpersonationClaims(
        IEnumerable<Claim> targetClaims, string jti, string actorUserId, string actorTenantId) =>
        targetClaims
            // Drop the target's locale: language is a presentation concern, so the operator reads in
            // THEIR own language (falls through to Accept-Language), not the impersonated user's.
            .Where(c => c.Type != JwtRegisteredClaimNames.Jti && c.Type != "locale")
            .Concat(
            [
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(ClaimConstants.ActorSubject, actorUserId),
                new Claim(ClaimConstants.ActorTenant, actorTenantId)
            ])
            .ToList();
}
