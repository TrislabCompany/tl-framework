using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Sessions;

/// <summary>
/// Shared helper for the Identity integration tests that need a *loginable* non-admin user. It goes the
/// product's way: the admin client invites a new address, and its owner accepts the e-mailed link with
/// their own password (which also confirms the e-mail). The user gets the seeded "Basic" role
/// (Sessions.View + Sessions.Revoke, but NOT the admin-only Sessions.ViewAll / Sessions.RevokeAll).
/// </summary>
internal static class IdentityUserSeeder
{
    /// <summary>Invites and accepts a new user in the admin client's tenant.</summary>
    public static async Task<SeededUser> CreateLoginableUserAsync(
        TrislabWebApplicationFactory factory,
        HttpClient adminClient,
        string prefix,
        string tenant = TestConstants.RootTenantId)
    {
        _ = tenant; // The admin client's own tenant receives the user.
        var user = await TestUsers.InviteAndAcceptAsync(factory, adminClient, prefix);
        return new SeededUser(user.UserId, user.Email, user.Password);
    }
}

internal sealed record SeededUser(string UserId, string Email, string Password);
