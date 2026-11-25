using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Authentication;

/// <summary>
/// Covers the authorization wiring around TOTP 2FA: the enroll endpoint shape, and the
/// login paths that reject missing or bad codes when 2FA is enabled. Happy-path tests
/// that require a matching TOTP code (verify-enroll success, login success with valid
/// code, disable flow) are intentionally NOT covered here — ASP.NET Identity's
/// AuthenticatorTokenProvider has no Generate method (apps produce codes, server only
/// validates), and recomputing the compatible code from the shared key in-test requires
/// a framework-compatible TOTP implementation that is a significant side quest. Tracked
/// as backlog item 3.2b. Manual verification with a real authenticator app is routine.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class TwoFactorAuthTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public TwoFactorAuthTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Login_Should_Succeed_WithoutCode_When_TwoFactorIsNotEnabled()
    {
        var (email, password) = await CreateConfirmedUserAsync();

        var response = await LoginAsync(email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Enroll_Should_ReturnSharedKey_AndAuthenticatorUri()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        using var client = await SignInAsync(email, password);

        var response = await client.PostAsJsonAsync("/api/v1/identity/2fa/enroll", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var enrollment = await DeserializeAsync<TwoFactorEnrollmentResponse>(response);
        enrollment.ShouldNotBeNull();
        enrollment.SharedKey.ShouldNotBeNullOrWhiteSpace();
        enrollment.AuthenticatorUri.ShouldStartWith("otpauth://totp/");
        enrollment.AuthenticatorUri.ShouldContain("secret=");
    }

    [Fact]
    public async Task Login_Should_Return401_When_TwoFactorEnabled_And_NoCodeProvided()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        await EnableTwoFactorAsync(email);

        var response = await LoginAsync(email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Both React login pages branch on this code to show the authenticator step,
        // so it is a client contract: the detail text is localized, the code is not.
        (await ReadProblemCodeAsync(response)).ShouldBe("Identity.TwoFactorRequired");
    }

    [Fact]
    public async Task Login_Should_Return401_When_TwoFactorEnabled_And_WrongCodeProvided()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        await EnableTwoFactorAsync(email);

        var response = await LoginAsync(email, password, "000000");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadProblemCodeAsync(response)).ShouldBe("Identity.TwoFactorInvalid");
    }

    [Fact]
    public async Task VerifyEnroll_Should_RejectInvalidCode()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        using var client = await SignInAsync(email, password);
        (await client.PostAsJsonAsync("/api/v1/identity/2fa/enroll", new { })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/identity/2fa/verify", new { code = "000000" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await IsTwoFactorEnabledAsync(email)).ShouldBeFalse();
    }

    [Fact]
    public async Task Disable_Should_Require_CurrentPassword()
    {
        var (email, password) = await CreateConfirmedUserAsync();
        await EnableTwoFactorAsync(email);

        // Log in WITHOUT 2FA enforcement for simplicity of the negative test — set
        // TwoFactorEnabled=false temporarily so we can acquire a token, then re-enable.
        await SetTwoFactorAsync(email, enabled: false);
        using var client = await SignInAsync(email, password);
        await SetTwoFactorAsync(email, enabled: true);

        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/2fa/disable",
            new { currentPassword = "NotThePassword!1" });
        wrongPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await IsTwoFactorEnabledAsync(email)).ShouldBeTrue();
    }

    // ---- helpers ----

    private async Task<(string Email, string Password)> CreateConfirmedUserAsync()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var user = await TestUsers.InviteAndAcceptAsync(_factory, rootClient, "2fa", "TwoFactor", "Test");
        var email = user.Email;
        var password = user.Password;

        return (email, password);
    }

    private async Task<HttpClient> SignInAsync(string email, string password, string? twoFactorCode = null)
    {
        using var loginClient = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email, password, twoFactorCode });
        var response = await loginClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var token = await DeserializeAsync<TokenResult>(response);
        token.ShouldNotBeNull();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);
        return client;
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password, string? twoFactorCode = null)
    {
        using var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email, password, twoFactorCode });
        return await client.SendAsync(request);
    }

    private async Task EnableTwoFactorAsync(string email)
    {
        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            await userManager.ResetAuthenticatorKeyAsync(user);
            await userManager.SetTwoFactorEnabledAsync(user, true);
        });
    }

    private async Task SetTwoFactorAsync(string email, bool enabled)
    {
        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            await userManager.SetTwoFactorEnabledAsync(user, enabled);
        });
    }

    private async Task<bool> IsTwoFactorEnabledAsync(string email)
    {
        bool enabled = false;
        await WithRootTenantScopeAsync(async userManager =>
        {
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            enabled = await userManager.GetTwoFactorEnabledAsync(user);
        });
        return enabled;
    }

    private async Task WithRootTenantScopeAsync(Func<UserManager<TrislabUser>, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo, string>>()
            .GetAsync(TestConstants.RootTenantId);
        using var tenantUse = scope.ServiceProvider.GetRequiredService<ITenantAccessor>().Use(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
        await action(userManager);
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
