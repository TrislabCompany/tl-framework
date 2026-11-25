using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Mailing;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Text;

namespace Trislab.Modules.Identity.Services;

internal sealed class UserPasswordService(
    UserManager<TrislabUser> userManager,
    IdentityDbContext db,
    IBackgroundMailer mailer,
    ITenantAccessor multiTenantContextAccessor,
    IPasswordHistoryService passwordHistoryService,
    IdentityAuditor auditor,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<SelfRegistrationOptions> responseOptions) : IUserPasswordService
{
    public async Task ForgotPasswordAsync(string email, string origin, CancellationToken cancellationToken)
    {
        // The same answer and the same least duration whether or not the address has an account.
        var started = timeProvider.GetTimestamp();
        try
        {
            await SendResetLinkAsync(email, origin).ConfigureAwait(false);
        }
        finally
        {
            await ResponseTime.PadAsync(timeProvider, started, responseOptions.Value.MinimumResponseTime, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendResetLinkAsync(string email, string origin)
    {
        // The password belongs to the global account, so no tenant is needed: the store looks the e-mail
        // up across all accounts.
        var user = await userManager.FindByEmailAsync(email);

        // Anti-enumeration: respond identically regardless of registration — a real user gets the
        // reset email; an unknown or email-less account silently no-ops with the same 200.
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // Build the SPA reset link with QueryHelpers (matches GetEmailVerificationUriAsync): trim any trailing
        // slash from the configured origin (Uri.ToString adds one for a host-only URL → "//reset-password"
        // misses the client route) and include the tenant the reset page requires. QueryHelpers URL-encodes
        // each value, so reserved chars in the email (e.g. '+') survive.
        var query = new Dictionary<string, string?>
        {
            ["token"] = token,
            ["email"] = email,
        };
        if (multiTenantContextAccessor?.TenantInfo?.Id is { Length: > 0 } tenantId)
        {
            query["tenant"] = tenantId;
        }

        var resetPasswordUri = QueryHelpers.AddQueryString($"{origin.TrimEnd('/')}/reset-password", query);
        // The body is sent as text/html, so the link has to be an anchor: a bare URL in an HTML part is
        // not auto-linked by most clients, which is how the reset link reached users as dead text.
        var mailRequest = new MailRequest(
            new Collection<string> { user.Email },
            "Reset Password",
            HtmlEmail.LinkAction(
                heading: "Reset your password",
                intro: "Use the link below to choose a new password.",
                actionUrl: resetPasswordUri,
                actionLabel: "Reset password"),
            textBody: $"Please reset your password using the following link: {resetPasswordUri}");

        mailer.Enqueue(mailRequest);
    }

    public async Task ResetPasswordAsync(string email, string password, string token, CancellationToken cancellationToken)
    {
        // The same least duration whatever the outcome: the timing must not tell an unknown address from a bad token.
        var started = timeProvider.GetTimestamp();
        try
        {
            await ResetPasswordCoreAsync(email, password, token, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ResponseTime.PadAsync(timeProvider, started, responseOptions.Value.MinimumResponseTime, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task ResetPasswordCoreAsync(string email, string password, string token, CancellationToken cancellationToken)
    {
        // The password belongs to the global account, so the reset needs no tenant. An unknown address fails
        // exactly like a wrong token: the reset must not reveal which addresses have accounts.
        var user = await userManager.FindByEmailAsync(email);
        string? decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            decodedToken = null;
        }

        // The token is checked before anything else, so only the mailbox owner learns whether the new
        // password was used before.
        if (user is null || decodedToken is null || !await userManager.VerifyUserTokenAsync(
                user, userManager.Options.Tokens.PasswordResetTokenProvider, UserManager<TrislabUser>.ResetPasswordTokenPurpose, decodedToken))
        {
            throw ResetFailed([userManager.ErrorDescriber.InvalidToken().Description]);
        }

        if (await passwordHistoryService.IsPasswordInHistoryAsync(user.Id, password, cancellationToken).ConfigureAwait(false))
        {
            throw new CustomException("password used recently", [], System.Net.HttpStatusCode.BadRequest)
                .Localized("Validation.PasswordRecentlyUsed");
        }

        // All or nothing: a reset that stopped half-way would leave a new password with the old sessions still alive.
        var tenantId = multiTenantContextAccessor?.TenantInfo?.Id;
        var revoked = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var result = await userManager.ResetPasswordAsync(user, decodedToken, password);
            if (!result.Succeeded)
            {
                throw ResetFailed(result.Errors.Select(e => e.Description).ToList());
            }

            // The reset link reached the mailbox: that confirms the e-mail, and it ends a lockout.
            user.EmailConfirmed = true;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            user.LastPasswordChangeDate = timeProvider.GetUtcNow().UtcDateTime;
            user.RecordPasswordChanged(wasReset: true, tenantId);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await passwordHistoryService.SavePasswordHistoryAsync(user.Id, cancellationToken).ConfigureAwait(false);

            // The reset rotated the security stamp, so no session could refresh anyway; ending them now
            // makes the session list honest at once, in every tenant of the account.
            revoked = await SessionRevocation.RevokeAsync(
                db, user.Id, tenantId: null, user.Id, "PasswordReset", timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await auditor.WriteForAccountAsync(SecurityAction.PasswordReset, user.Id, "PasswordReset",
            new Dictionary<string, object?> { ["sessionsRevoked"] = revoked }, ct: cancellationToken).ConfigureAwait(false);
    }

    private static CustomException ResetFailed(IReadOnlyCollection<string> errors) =>
        new CustomException("error resetting password", errors.ToList(), System.Net.HttpStatusCode.BadRequest)
            .Localized<IdentityResources>("Identity.ErrorResettingPassword");

    public async Task ChangePasswordAsync(string password, string newPassword, string confirmNewPassword, string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("user not found").Localized<IdentityResources>("Identity.UserNotFound");

        // All or nothing, like the reset: a change that stopped half-way would leave the old sessions alive.
        var tenantId = multiTenantContextAccessor?.TenantInfo?.Id;
        var revoked = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var result = await userManager.ChangePasswordAsync(user, password, newPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                throw new CustomException("failed to change password", errors, System.Net.HttpStatusCode.BadRequest)
                    .Localized<IdentityResources>("Identity.FailedToChangePassword");
            }

            user.LastPasswordChangeDate = timeProvider.GetUtcNow().UtcDateTime;
            user.RecordPasswordChanged(wasReset: false, tenantId);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await passwordHistoryService.SavePasswordHistoryAsync(user.Id, cancellationToken).ConfigureAwait(false);

            // The change rotated the security stamp, so no session could refresh anyway; ending them now makes
            // the session list honest at once, in every tenant of the account.
            revoked = await SessionRevocation.RevokeAsync(
                db, user.Id, tenantId: null, user.Id, "PasswordChanged", timeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await auditor.WriteForAccountAsync(SecurityAction.PasswordChanged, user.Id, "PasswordChanged",
            new Dictionary<string, object?> { ["sessionsRevoked"] = revoked }, ct: cancellationToken).ConfigureAwait(false);
    }
}