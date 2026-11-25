using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Authentication;

[Collection(TrislabCollectionDefinition.Name)]
public sealed class AccountLockoutTests
{
    private const int MaxFailedAttempts = 5;
    private const HttpStatusCode HttpLocked = (HttpStatusCode)423;

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public AccountLockoutTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Login_Should_LockAccount_After_ConsecutiveFailedAttempts()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var (email, _) = await CreateConfirmedUserAsync(rootClient);

        // Five wrong-password attempts must all return 401 (generic auth failure).
        for (int attempt = 1; attempt <= MaxFailedAttempts; attempt++)
        {
            var response = await AttemptLoginAsync(email, "WrongPassword!1");
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
                $"Attempt {attempt} should return 401 before the lockout threshold is crossed.");
        }

        // Subsequent attempts (even with the CORRECT password) must be rejected with 423 Locked.
        var afterLockout = await AttemptLoginAsync(email, "Test@1234!");
        afterLockout.StatusCode.ShouldBe(HttpLocked);
    }

    [Fact]
    public async Task Login_Should_ResetFailedCount_After_SuccessfulAuthentication()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var (email, password) = await CreateConfirmedUserAsync(rootClient);

        // Three wrong attempts — below the 5-attempt threshold.
        for (int i = 0; i < 3; i++)
        {
            (await AttemptLoginAsync(email, "WrongPassword!1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // A correct login must succeed AND reset the counter.
        var successResponse = await AttemptLoginAsync(email, password);
        successResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // After the reset, another 4 wrong attempts should still only yield 401 — not 423.
        // If the counter weren't reset, attempt 2 below would cross the threshold and lock.
        for (int i = 0; i < 4; i++)
        {
            (await AttemptLoginAsync(email, "WrongPassword!1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private async Task<(string Email, string Password)> CreateConfirmedUserAsync(HttpClient rootClient)
    {
        var user = await TestUsers.InviteAndAcceptAsync(_factory, rootClient, "lockout", "Lockout", "Test");
        return (user.Email, user.Password);
    }

    private async Task<HttpResponseMessage> AttemptLoginAsync(string email, string password)
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email, password });
        return await client.SendAsync(request);
    }
}
