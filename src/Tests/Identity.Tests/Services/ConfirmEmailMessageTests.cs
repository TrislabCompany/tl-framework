using System.Globalization;
using System.Text;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Eventing.Outbox;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Identity.Services;
using Identity.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace Identity.Tests.Services;

/// <summary>
/// The confirm-email success text is rendered verbatim by both SPAs' confirmation page, so it has to
/// follow the caller's language like every other server message.
/// </summary>
public sealed class ConfirmEmailMessageTests
{
    [Theory]
    [InlineData("en-US", "Your email u@codefi.com.br is confirmed. You can now sign in.")]
    [InlineData("sl-SI", "Vaša e-pošta u@codefi.com.br je potrjena. Zdaj se lahko prijavite.")]
    public async Task ConfirmEmailAsync_returns_the_success_message_in_the_request_language(string culture, string expected)
    {
        // Arrange
        var user = new TrislabUser { Id = "u1", Email = "u@codefi.com.br", UserName = "u", EmailConfirmed = false, PasswordHash = "hash" };
        var userManager = Substitute.For<UserManager<TrislabUser>>(
            Substitute.For<IUserStore<TrislabUser>>(), null, null, null, null, null, null, null, null);
        userManager.Users.Returns(new[] { user }.AsAsyncQueryable());
        userManager.ConfirmEmailAsync(user, "token").Returns(IdentityResult.Success);
        userManager.VerifyUserTokenAsync(user, Arg.Any<string>(), UserManager<TrislabUser>.ConfirmEmailTokenPurpose, "token").Returns(true);
        userManager.CheckPasswordAsync(user, "Secret@1234!").Returns(true);

        var tenant = Substitute.For<ITenantAccessor>();
        tenant.TenantInfo.Returns(new AppTenantInfo("root", "root"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "");
        var localizer = services.BuildServiceProvider().GetRequiredService<IStringLocalizer<IdentityResources>>();

        var sut = new UserRegistrationService(
            userManager,
            db: null!,
            Substitute.For<IBackgroundMailer>(),
            tenant,
            Substitute.For<IOutboxStore>(),
            null!,
            TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new SelfRegistrationOptions { MinimumResponseTime = TimeSpan.Zero }),
            localizer);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("token"));

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            // Act
            var message = await sut.ConfirmEmailAsync("u1", code, "Secret@1234!", "root", CancellationToken.None);

            // Assert
            message.ShouldBe(expected);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
