using System.Security.Claims;

namespace Trislab.Modules.Identity.Contracts.Services;

public interface IIdentityService
{
    /// <summary>
    /// Checks an account's e-mail, password and, when the account has one, its second factor. Accounts are
    /// global, so no tenant is involved. Lockout and the confirmed e-mail are checked here too.
    /// </summary>
    /// <param name="email">The account's e-mail address.</param>
    /// <param name="password">The account's password.</param>
    /// <param name="twoFactorCode">The authenticator code, when the account has a second factor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user id. Wrong credentials throw a 401.</returns>
    Task<string> ValidateCredentialsAsync(string email, string password, string? twoFactorCode = null, CancellationToken ct = default);

    /// <summary>
    /// Builds the claim set of a user in a tenant: the user's own claims, the tenant claim and the roles the
    /// user has there (direct and through groups). Runs in that tenant's scope, whatever the current request's
    /// tenant, so impersonation and end-impersonation use it too. Returns null when the user is not a member
    /// of the tenant; a deactivated membership throws a 401.
    /// </summary>
    Task<(string Subject, IEnumerable<Claim> Claims)?>
        BuildClaimsForUserAsync(string userId, string tenantId, CancellationToken ct = default);
}
