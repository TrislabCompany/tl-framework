using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Mailing;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Identity.Tests.Services;

/// <summary>
/// Tests for UserPasswordService.ForgotPasswordAsync — focuses on the reset-link format
/// (regression cover for the double-slash, missing-tenant and unencoded-email defects).
/// </summary>
public sealed class UserPasswordServiceTests
{
    private const string TenantId = "codefi";

    private readonly UserManager<TrislabUser> _userManager;
    private readonly IBackgroundMailer _mailer;
    private readonly ITenantAccessor _tenantAccessor;

    public UserPasswordServiceTests()
    {
        _userManager = Substitute.For<UserManager<TrislabUser>>(
            Substitute.For<IUserStore<TrislabUser>>(), null, null, null, null, null, null, null, null);
        _mailer = Substitute.For<IBackgroundMailer>();
        _tenantAccessor = Substitute.For<ITenantAccessor>();

        _tenantAccessor.TenantInfo.Returns(new AppTenantInfo(TenantId, TenantId, "Codefi"));
    }

    private UserPasswordService CreateSut() =>
        new(_userManager, null!, _mailer, _tenantAccessor, null!, null!, TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new SelfRegistrationOptions { MinimumResponseTime = TimeSpan.Zero }));

    private MailRequest CaptureSentMail()
    {
        var call = _mailer.ReceivedCalls().Single();
        return (MailRequest)call.GetArguments()[0]!;
    }

    [Fact]
    public async Task ForgotPasswordAsync_Should_BuildResetLink_WithSingleSlash_Tenant_AndEncodedEmail()
    {
        // Arrange — trailing slash on the origin (as Uri.ToString() produces for a host-only URL) and an
        // email with reserved characters ('+', '@') to exercise all three defects at once.
        const string email = "marcelo+reset@codefi.com.br";
        var user = new TrislabUser { Email = email, UserName = email };
        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("raw-token");

        var sut = CreateSut();

        // Act
        await sut.ForgotPasswordAsync(email, "https://appbase.codefi.com.br/", CancellationToken.None);

        // Assert — the link is asserted on the text/plain alternative, which carries the URL verbatim.
        // The HTML part necessarily escapes '&' as '&amp;' (see the anchor test below).
        var body = CaptureSentMail().TextBody!;
        body.ShouldContain("https://appbase.codefi.com.br/reset-password?");
        body.ShouldNotContain("//reset-password");                       // defect 3: no double slash
        body.ShouldContain($"&tenant={TenantId}");                       // defect 4: tenant present
        // defect 5: reserved chars are encoded — '+' must become %2B (an unencoded '+' would decode to a
        // space). '@' is left as-is, which is valid in a query component per RFC 3986 (QueryHelpers encodes
        // only what is required, matching GetEmailVerificationUriAsync).
        body.ShouldContain("email=marcelo%2Breset");
        body.ShouldNotContain("email=marcelo+reset");                    // raw '+' must not leak
    }

    [Fact]
    public async Task ForgotPasswordAsync_Should_SendTheLinkAsAnAnchor_Not_BareText()
    {
        // Arrange — the body travels as text/html (BodyBuilder.HtmlBody / SendGrid htmlContent), and most
        // clients do not auto-link a bare URL inside an HTML part: it reaches the user as dead text.
        const string email = "operator@codefi.com.br";
        var user = new TrislabUser { Email = email, UserName = email };
        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("raw-token");

        var sut = CreateSut();

        // Act
        await sut.ForgotPasswordAsync(email, "https://adminbase.codefi.com.br", CancellationToken.None);

        // Assert
        var html = CaptureSentMail().Body!;
        html.ShouldContain("<a href=\"https://adminbase.codefi.com.br/reset-password?");
        // Inside HTML the query separator must be the entity, or the parser eats the following parameter.
        html.ShouldContain("&amp;tenant=");
        html.ShouldNotContain("&tenant=");
    }

    [Fact]
    public async Task ForgotPasswordAsync_Should_SendATextAlternative_AlongsideTheHtml()
    {
        // Arrange — HTML-only mail leaves text-only clients with nothing and scores worse with spam filters.
        const string email = "operator@codefi.com.br";
        var user = new TrislabUser { Email = email, UserName = email };
        _userManager.FindByEmailAsync(email).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("raw-token");

        var sut = CreateSut();

        // Act
        await sut.ForgotPasswordAsync(email, "https://adminbase.codefi.com.br", CancellationToken.None);

        // Assert
        var mail = CaptureSentMail();
        mail.TextBody.ShouldNotBeNullOrWhiteSpace();
        mail.TextBody.ShouldContain("https://adminbase.codefi.com.br/reset-password?");
        mail.TextBody.ShouldNotContain("<a ");
        mail.TextBody.ShouldNotContain("&amp;");
    }

    [Fact]
    public async Task ForgotPasswordAsync_Should_NotEnqueueMail_When_UserIsUnknown()
    {
        // Arrange — anti-enumeration: unknown user silently no-ops (no mail), still a 200 upstream.
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((TrislabUser?)null);
        var sut = CreateSut();

        // Act
        await sut.ForgotPasswordAsync("ghost@codefi.com.br", "https://appbase.codefi.com.br/", CancellationToken.None);

        // Assert
        _mailer.DidNotReceive().Enqueue(Arg.Any<MailRequest>());
    }
}
