using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Integration.Tests.Tests.Authentication;

/// <summary>
/// Forgot/reset-password flow. The reset token is normally emailed (NoOp in tests),
/// so the token is generated through <see cref="UserManager{TUser}"/> exactly as the
/// production service does, then exercised through the public endpoint.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class PasswordResetTests
{
    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public PasswordResetTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    #region Happy Path

    [Fact]
    public async Task ResetPassword_Should_SetNewPassword_When_TokenIsValid()
    {
        // Arrange
        var email = $"reset_ok_{Guid.NewGuid():N}@test.com";
        const string oldPassword = TestConstants.DefaultPassword;
        const string newPassword = "NewPa$$word123!";
        await CreateActiveUserAsync(email, oldPassword);
        var encodedToken = await GenerateResetTokenAsync(email);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password",
            new { email, password = newPassword, token = encodedToken });

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        // The new password authenticates...
        var token = await _auth.GetTokenAsync(email, newPassword, TestConstants.RootTenantId);
        token.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // ...and the old one no longer does.
        await Should.ThrowAsync<HttpRequestException>(
            () => _auth.GetTokenAsync(email, oldPassword, TestConstants.RootTenantId));
    }

    #endregion

    #region Exception / Edge Cases

    [Fact]
    public async Task ResetPassword_Should_Fail_And_LeavePasswordUnchanged_When_TokenIsInvalid()
    {
        // Arrange
        var email = $"reset_badtoken_{Guid.NewGuid():N}@test.com";
        const string oldPassword = TestConstants.DefaultPassword;
        await CreateActiveUserAsync(email, oldPassword);
        var garbageToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("not-a-real-token"));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password",
            new { email, password = "Whatever123!", token = garbageToken });

        // Assert — request is rejected and, critically, nothing changed.
        ((int)response.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
        var token = await _auth.GetTokenAsync(email, oldPassword, TestConstants.RootTenantId);
        token.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResetPassword_Should_Fail_Like_A_Bad_Token_When_EmailIsUnknown()
    {
        // Arrange
        var garbageToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("token"));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password",
            new { email = $"ghost_{Guid.NewGuid():N}@test.com", password = "Whatever123!", token = garbageToken });

        // Assert: the same 400 as a wrong token, so the reset can't tell which addresses have accounts.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Identity.ErrorResettingPassword");
    }

    #endregion

    #region Helpers

    // Set the tenant context INLINE, not via an awaited helper: the Finbuckle setter writes an AsyncLocal
    // that's lost on return from a separate method, NREing the tenant query filter during CreateAsync.
    private async Task CreateActiveUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
        var user = new TrislabUser
        {
            FirstName = "Reset",
            LastName = "User",
            Email = email,
            UserName = email.Split('@')[0],
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Users are global: the membership is what puts the user in the tenant.
        var identityDb = scope.ServiceProvider.GetRequiredService<Trislab.Modules.Identity.Data.IdentityDbContext>();
        identityDb.TenantMemberships.Add(TenantMembership.CreateAccepted(user.Id, TestConstants.RootTenantId));
        await identityDb.SaveChangesAsync();
    }

    private async Task<string> GenerateResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var raw = await userManager.GeneratePasswordResetTokenAsync(user);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
    }

    #endregion
}
