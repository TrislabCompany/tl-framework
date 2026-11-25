namespace Trislab.Modules.Identity.Contracts.DTOs;

/// <summary>
/// The answer to a sign-in. Either the tokens for one tenant (<see cref="TenantSelectionRequired"/> false),
/// or, for a user with several tenants and no default, the list to choose from and a short-lived
/// <see cref="SelectionToken"/> to send with the choice to <c>POST /identity/token/select</c>.
/// </summary>
/// <param name="AccessToken">The access token, or null while a tenant has to be chosen.</param>
/// <param name="RefreshToken">The refresh token, or null while a tenant has to be chosen.</param>
/// <param name="RefreshTokenExpiresAt">When the refresh token expires.</param>
/// <param name="AccessTokenExpiresAt">When the access token expires.</param>
/// <param name="Tenant">The tenant the tokens are for.</param>
/// <param name="TenantSelectionRequired">True when the user has to choose a tenant first.</param>
/// <param name="SelectionToken">Proof of the sign-in for the choice; valid for a few minutes.</param>
/// <param name="Tenants">The tenants the user can choose from.</param>
public sealed record SignInResponse(
    string? AccessToken,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    DateTime? AccessTokenExpiresAt,
    string? Tenant,
    bool TenantSelectionRequired,
    string? SelectionToken,
    IReadOnlyList<TenantMembershipDto>? Tenants)
{
    public static SignInResponse Issued(TokenResponse token, string tenant)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new(token.AccessToken, token.RefreshToken, token.RefreshTokenExpiresAt, token.AccessTokenExpiresAt,
            tenant, TenantSelectionRequired: false, SelectionToken: null, Tenants: null);
    }

    public static SignInResponse ChooseTenant(string selectionToken, IReadOnlyList<TenantMembershipDto> tenants) =>
        new(null, null, null, null, null, TenantSelectionRequired: true, selectionToken, tenants);
}

/// <summary>A tenant the user is an active member of.</summary>
/// <param name="Id">The tenant id, sent as the <c>tenant</c> of a choice or a switch.</param>
/// <param name="Name">The tenant's display name.</param>
/// <param name="IsDefault">True for the tenant sign-in opens by default.</param>
public sealed record TenantMembershipDto(string Id, string? Name, bool IsDefault);
