using System.Text.RegularExpressions;
using Trislab.Modules.Identity.Data;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// Creates users the way a product does: an administrator invites the address and its owner accepts the
/// e-mailed link with their own password. The test reads the link from the captured e-mail.
/// </summary>
internal static partial class TestUsers
{
    public const string DefaultUserPassword = "Test@1234!";

    /// <summary>Invites a new address into the admin client's tenant and accepts it; the user can sign in.</summary>
    public static async Task<InvitedUser> InviteAndAcceptAsync(
        TrislabWebApplicationFactory factory,
        HttpClient adminClient,
        string prefix,
        string firstName = "Seeded",
        string lastName = "User",
        string password = DefaultUserPassword)
    {
        var email = $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}@example.com";
        await InviteAsync(adminClient, email, firstName, lastName);
        var token = await WaitForInvitationTokenAsync(email);

        using var anonymous = factory.CreateClient();
        using var accept = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password, firstName, lastName });
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());

        return new InvitedUser(await FindUserIdAsync(factory, email), email, password);
    }

    /// <summary>Sends an invitation; returns the response for the caller to check.</summary>
    public static async Task InviteAsync(HttpClient adminClient, string email, string? firstName = null, string? lastName = null)
    {
        using var response = await adminClient.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/invitations", new { email, firstName, lastName });
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Waits for an invitation e-mail to <paramref name="email"/> beyond the first <paramref name="previousCount"/> and returns its token.</summary>
    public static async Task<string> WaitForInvitationTokenAsync(string email, int previousCount = 0)
    {
        var message = await Mailpit.WaitForMessageAsync(email, m => TokenPattern().IsMatch(m.Body), skip: previousCount);
        return Uri.UnescapeDataString(TokenPattern().Match(message.Body).Groups[1].Value);
    }

    /// <summary>How many invitation e-mails <paramref name="email"/> received so far.</summary>
    public static async Task<int> InvitationCountAsync(string email) =>
        (await Mailpit.MessagesToAsync(email)).Count(m => TokenPattern().IsMatch(m.Body));

    /// <summary>The account id of an address, read across tenants.</summary>
    public static async Task<string> FindUserIdAsync(TrislabWebApplicationFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var normalized = email.ToUpperInvariant();
        return await db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => u.Id)
            .SingleAsync();
    }

    [GeneratedRegex(@"accept-invitation\?token=([^&\s""<]+)")]
    private static partial Regex TokenPattern();
}

internal sealed record InvitedUser(string UserId, string Email, string Password);
