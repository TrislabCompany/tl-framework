using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

namespace Trislab.Modules.Identity.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<TrislabUser> _userManager;
    private readonly ILogger<IdentityService> _logger;
    private readonly IEventTenantScope _tenantScope;
    private readonly IServiceScopeFactory _scopeFactory;

    public IdentityService(
        UserManager<TrislabUser> userManager,
        ILogger<IdentityService> logger,
        IEventTenantScope tenantScope,
        IServiceScopeFactory scopeFactory)
    {
        _userManager = userManager;
        _logger = logger;
        _tenantScope = tenantScope;
        _scopeFactory = scopeFactory;
    }

    public async Task<string> ValidateCredentialsAsync(string email, string password, string? twoFactorCode = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var user = await FindAndValidateUserByCredentialsAsync(email, password);

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException("email not confirmed")
                .Localized<IdentityResources>("Identity.EmailNotConfirmed");
        }

        if (user.TwoFactorEnabled)
        {
            await VerifyTwoFactorOrThrowAsync(user, twoFactorCode);
        }

        return user.Id;
    }

    private async Task VerifyTwoFactorOrThrowAsync(TrislabUser user, string? twoFactorCode)
    {
        if (string.IsNullOrWhiteSpace(twoFactorCode))
        {
            throw new CustomException(
                "two_factor_required: An authenticator code is required to complete sign-in.",
                errors: null,
                HttpStatusCode.Unauthorized)
                .Localized<IdentityResources>("Identity.TwoFactorRequired");
        }

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            twoFactorCode);

        if (!valid)
        {
            _logger.LogWarning("Invalid two-factor code for user {UserId}", user.Id);
            throw new UnauthorizedException("two_factor_invalid: The authenticator code is invalid or expired.")
                .Localized<IdentityResources>("Identity.TwoFactorInvalid");
        }
    }

    public async Task<(string Subject, IEnumerable<Claim> Claims)?>
        BuildClaimsForUserAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(tenantId);

        // Roles, groups and the membership are tenant-scoped, so read them in the target tenant's own scope:
        // the tenant filters then do the isolation, whatever tenant the current request is in.
        (string Subject, IEnumerable<Claim> Claims)? result = null;
        await _tenantScope.ExecuteAsync(tenantId, async token =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            result = await BuildClaimsInScopeAsync(scope.ServiceProvider, userId, tenantId, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Builds a member's claims with the services of a scope that is already in <paramref name="tenantId"/>.
    /// Returns null when the user is not a member there; a deactivated membership throws a 401.
    /// </summary>
    internal static async Task<(string Subject, IEnumerable<Claim> Claims)?> BuildClaimsInScopeAsync(
        IServiceProvider services, string userId, string tenantId, CancellationToken ct)
    {
        var db = services.GetRequiredService<IdentityDbContext>();

        var member = await db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.IsActive, m.User })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (member?.User is null)
        {
            return null;
        }

        if (!member.IsActive)
        {
            throw new UnauthorizedException("user is deactivated")
                .Localized<IdentityResources>("Identity.UserDeactivated");
        }

        var user = member.User;
        var userManager = services.GetRequiredService<UserManager<TrislabUser>>();
        var groupRoles = services.GetRequiredService<IGroupRoleService>();

        var claims = CreateBasicClaims(user, tenantId);
        var directRoles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var viaGroups = await groupRoles.GetUserGroupRolesAsync(user.Id, ct).ConfigureAwait(false);
        claims.AddRange(directRoles.Union(viaGroups).Distinct().Select(r => new Claim(ClaimTypes.Role, r)));

        return (user.Id, claims);
    }

    private async Task<TrislabUser> FindAndValidateUserByCredentialsAsync(string email, string password)
    {
        // The store looks e-mail up across all accounts: an account is global, not a tenant's.
        var user = await _userManager.FindByEmailAsync(email.Trim().Normalize());
        if (user is null)
        {
            // Generic 401 — never confirm or deny account existence from this path.
            throw new UnauthorizedException();
        }

        // Lockout check runs BEFORE password check so an attacker can't tell a locked
        // account from a wrong-password one on every request.
        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login attempted for locked account {UserId}", user.Id);
            throw new CustomException(
                "Account is temporarily locked due to too many failed login attempts. Try again later.",
                errors: null,
                HttpStatusCode.Locked)
                .Localized<IdentityResources>("Identity.AccountLocked");
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            if (_userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning(
                        "Account {UserId} locked out after exceeding failed login threshold.",
                        user.Id);
                }
            }
            throw new UnauthorizedException();
        }

        // Successful authentication resets the failed-attempt counter.
        if (_userManager.SupportsUserLockout && await _userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        return user;
    }

    internal static List<Claim> CreateBasicClaims(TrislabUser user, string tenantId)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // RFC 7519 short-form sub/name/email emitted alongside legacy ClaimTypes.* so JWT consumers read them per spec.
            // `name` is published explicitly because the default outbound map turns ClaimTypes.Name into `unique_name`, not `name`.
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, fullName.Length > 0 ? fullName : (user.Email ?? string.Empty)),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FirstName ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimConstants.Fullname, fullName),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new(ClaimConstants.Tenant, tenantId),
            new(ClaimConstants.ImageUrl, user.ImageUrl?.ToString() ?? string.Empty)
        };

        // OIDC-standard `locale` claim, emitted ONLY when the user explicitly chose a language.
        // A null/blank Locale emits no claim so the culture-resolution chain falls to Accept-Language
        // rather than forcing the deployment default onto users who never picked one.
        if (!string.IsNullOrWhiteSpace(user.Locale))
        {
            claims.Add(new Claim("locale", user.Locale));
        }

        return claims;
    }
}
