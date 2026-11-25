using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Common;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Eventing.Outbox;
using Trislab.Framework.Mailing;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Contracts.Events;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Trislab.Modules.Auditing.Contracts;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text;

namespace Trislab.Modules.Identity.Services;

internal sealed class UserRegistrationService(
    UserManager<TrislabUser> userManager,
    IdentityDbContext db,
    IBackgroundMailer mailer,
    ITenantAccessor multiTenantContextAccessor,
    IOutboxStore outboxStore,
    IdentityAuditor auditor,
    TimeProvider timeProvider,
    IOptions<SelfRegistrationOptions> registrationOptions,
    IStringLocalizer<IdentityResources> localizer) : IUserRegistrationService
{
    /// <summary>
    /// Self-registration in the current tenant. It never tells whether the address has an account: the answer
    /// and its timing are the same either way. A new address gets an account and a confirmation e-mail; an
    /// address with an account gets an e-mail telling its owner, and nothing else changes.
    /// </summary>
    public async Task RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string userName,
        string password,
        string confirmPassword,
        string phoneNumber,
        string origin,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var tenantId = multiTenantContextAccessor.TenantInfo?.Id
            ?? throw new UnauthorizedException("invalid tenant").Localized("Error.InvalidTenant");

        // The operator console has no sign-up: operators are invited by an operator.
        if (string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            throw new ForbiddenException("self-registration is not open for the operator console")
                .Localized<IdentityResources>("Identity.SelfRegistrationClosed");
        }

        ValidatePasswordMatch(password, confirmPassword);

        // Check the password policy before looking the address up, so a weak password fails the same way for both.
        var candidate = new TrislabUser { Email = email, UserName = userName };
        await EnsurePasswordPolicyAsync(candidate, password).ConfigureAwait(false);

        var existing = await userManager.FindByEmailAsync(userManager.NormalizeEmail(email)).ConfigureAwait(false);
        if (existing is not null)
        {
            // Spend the same work as creating an account (the password hash), then tell the owner by e-mail.
            _ = userManager.PasswordHasher.HashPassword(candidate, password);
            SendExistingAccountNotice(existing, origin);
            await auditor.WriteAsync(
                SecurityAction.PolicyFailed,
                existing.Id,
                "SelfRegistrationExistingAccount",
                new Dictionary<string, object?> { ["tenant"] = tenantId },
                AuditSeverity.Information,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            userName = await UniqueUserNameAsync(userName).ConfigureAwait(false);
            var user = await CreateUserWithPasswordAsync(firstName, lastName, email, userName, password, phoneNumber).ConfigureAwait(false);
            if (user is null)
            {
                // Another request created the address a moment ago: answer as for an existing account.
                await ResponseTime.PadAsync(timeProvider, started, registrationOptions.Value.MinimumResponseTime, cancellationToken).ConfigureAwait(false);
                return;
            }

            db.TenantMemberships.Add(TenantMembership.CreateAccepted(user.Id, tenantId));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await MembershipRules.AssignDefaultRoleAndGroupsAsync(userManager, db, user, "SelfRegistration", cancellationToken).ConfigureAwait(false);
            await SendConfirmationEmailAsync(user, origin).ConfigureAwait(false);
            await PublishUserRegisteredAsync(user, "Identity", cancellationToken).ConfigureAwait(false);
        }

        await ResponseTime.PadAsync(timeProvider, started, registrationOptions.Value.MinimumResponseTime, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ConfirmEmailAsync(string userId, string code, string password, string tenant, CancellationToken cancellationToken)
    {
        EnsureValidTenant();

        // The same least duration whatever went wrong, so the timing tells nothing either.
        var started = timeProvider.GetTimestamp();
        try
        {
            return await ConfirmEmailCoreAsync(userId, code, password, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ResponseTime.PadAsync(timeProvider, started, registrationOptions.Value.MinimumResponseTime, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<string> ConfirmEmailCoreAsync(string userId, string code, string password, CancellationToken cancellationToken)
    {
        // The e-mail belongs to the global account: the signed link is the proof, whatever the tenant.
        var user = await userManager.Users
            .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
            .Where(u => u.Id == userId && !u.EmailConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        string? decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            decoded = null;
        }

        // The code proves the mailbox, the password proves the person who signed up. Both are needed: someone who
        // registered another person's address can't have the account confirmed by its owner opening the link.
        // One generic failure for an unknown id, a confirmed account, a wrong code or a wrong password: the
        // anonymous endpoint must not tell them apart, nor echo an address.
        if (user is null || decoded is null || string.IsNullOrEmpty(user.PasswordHash)
            || (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
            || !await userManager.VerifyUserTokenAsync(
                user, userManager.Options.Tokens.EmailConfirmationTokenProvider, UserManager<TrislabUser>.ConfirmEmailTokenPurpose, decoded))
        {
            throw ConfirmationFailed();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            // A wrong password counts towards the lockout, as at sign-in, so the link can't be used to guess it.
            if (userManager.SupportsUserLockout)
            {
                await userManager.AccessFailedAsync(user);
            }

            throw ConfirmationFailed();
        }

        var result = await userManager.ConfirmEmailAsync(user, decoded);
        return result.Succeeded
            ? localizer["Identity.EmailConfirmed", user.Email!].Value
            : throw ConfirmationFailed();
    }

    private static CustomException ConfirmationFailed() =>
        new CustomException("An error occurred while confirming E-Mail.", errors: null, HttpStatusCode.BadRequest)
            .Localized<IdentityResources>("Identity.EmailConfirmationError");

    public async Task ResendConfirmationEmailAsync(string userId, string origin, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.")
                .Localized<IdentityResources>("Identity.UserNotFoundById", userId);

        if (user.EmailConfirmed)
        {
            throw new CustomException(string.Format(
                CultureInfo.InvariantCulture,
                "The email for {0} is already confirmed.",
                user.Email))
                .Localized<IdentityResources>("Identity.EmailAlreadyConfirmed", user.Email!);
        }

        await SendConfirmationEmailAsync(user, origin);
    }

    public async Task<string> ConfirmPhoneNumberAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        EnsureValidTenant();

        var user = await userManager.Users
            .Where(u => u.Id == userId && !u.PhoneNumberConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new CustomException("An error occurred while confirming phone number.")
            .Localized<IdentityResources>("Identity.PhoneConfirmationError");

        code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await userManager.ChangePhoneNumberAsync(user, user.PhoneNumber!, code);

        return result.Succeeded
            ? string.Format(CultureInfo.InvariantCulture, "Phone number {0} confirmed successfully.", user.PhoneNumber)
            : throw new CustomException(string.Format(CultureInfo.InvariantCulture, "An error occurred while confirming phone number {0}", user.PhoneNumber))
                .Localized<IdentityResources>("Identity.PhoneConfirmationFailedFor", user.PhoneNumber!);
    }

    private void EnsureValidTenant()
    {
        if (string.IsNullOrWhiteSpace(multiTenantContextAccessor?.TenantInfo?.Id))
        {
            throw new UnauthorizedException("invalid tenant").Localized("Error.InvalidTenant");
        }
    }

    private static void ValidatePasswordMatch(string password, string confirmPassword)
    {
        if (password != confirmPassword)
        {
            throw new CustomException(
                "Passwords do not match.",
                errors: null,
                HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.PasswordsDoNotMatch");
        }
    }

    private async Task<TrislabUser?> CreateUserWithPasswordAsync(
        string firstName,
        string lastName,
        string email,
        string userName,
        string password,
        string phoneNumber)
    {
        var user = new TrislabUser
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            UserName = userName,
            PhoneNumber = phoneNumber,
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded && result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return null;
        }

        if (!result.Succeeded)
        {
            // Identity create failures (duplicate email/username, password policy, …) are
            // client-input errors, not server faults — surface them as 400 with the specific
            // reasons so the caller sees *why* registration failed, not a bare 500.
            var errors = result.Errors.Select(error => error.Description).ToList();
            throw new CustomException(
                "Unable to register the user.",
                errors,
                HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.UnableToRegisterUser");
        }

        return user;
    }

    private async Task EnsurePasswordPolicyAsync(TrislabUser candidate, string password)
    {
        var errors = new List<string>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, candidate, password).ConfigureAwait(false);
            errors.AddRange(result.Errors.Select(e => e.Description));
        }

        if (errors.Count > 0)
        {
            throw new CustomException("Unable to register the user.", errors, HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.UnableToRegisterUser");
        }
    }

    // A user name is not secret, but "taken" would still tell a caller that someone signed up with it:
    // a taken name gets a short random suffix instead of an error.
    private async Task<string> UniqueUserNameAsync(string userName)
    {
        if (await userManager.FindByNameAsync(userManager.NormalizeName(userName)).ConfigureAwait(false) is null)
        {
            return userName;
        }

        return $"{userName}-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3))}";
    }

    private void SendExistingAccountNotice(TrislabUser existing, string origin)
    {
        if (string.IsNullOrEmpty(existing.Email))
        {
            return;
        }

        var signIn = $"{origin.TrimEnd('/')}/login";
        var mail = new MailRequest(
            new Collection<string> { existing.Email },
            "Someone tried to sign up with your address",
            HtmlEmail.LinkAction(
                heading: "You already have an account",
                intro: "Someone just tried to create an account with this e-mail address. You already have one: sign in, or reset your password if you forgot it. If it wasn't you, you can ignore this message.",
                actionUrl: signIn,
                actionLabel: "Sign in"),
            textBody: $"You already have an account with this address. Sign in: {signIn}");

        mailer.Enqueue(mail);
    }

    private async Task SendConfirmationEmailAsync(TrislabUser user, string origin)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
        string emailBody = BuildConfirmationEmailHtml(user.FirstName ?? user.UserName ?? "User", emailVerificationUri);

        var mailRequest = new MailRequest(
            new Collection<string> { user.Email },
            "Confirm Your Email Address",
            emailBody,
            textBody: $"Please confirm your email address using the following link: {emailVerificationUri}");

        mailer.Enqueue(mailRequest);
    }

    private async Task PublishUserRegisteredAsync(
        TrislabUser user,
        string source,
        CancellationToken cancellationToken = default)
    {
        var tenantId = multiTenantContextAccessor.TenantInfo?.Id;
        user.RecordRegistered(tenantId);

        await db.SaveChangesAsync(cancellationToken);

        var integrationEvent = new UserRegisteredIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: TimeProvider.System.GetUtcNow().UtcDateTime,
            TenantId: tenantId,
            CorrelationId: Guid.NewGuid().ToString(),
            Source: source,
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty);

        await outboxStore.AddAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetEmailVerificationUriAsync(TrislabUser user, string origin)
    {
        EnsureValidTenant();

        string code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        // Point at the SPA confirm-email page on the front-end the user registered from, which in turn
        // calls the API. The origin argument is the already-resolved front-end origin.
        const string route = "confirm-email";
        var endpointUri = new Uri(string.Concat($"{origin.TrimEnd('/')}/", route));

        string verificationUri = QueryHelpers.AddQueryString(endpointUri.ToString(), QueryStringKeys.UserId, user.Id);
        verificationUri = QueryHelpers.AddQueryString(verificationUri, QueryStringKeys.Code, code);
        verificationUri = QueryHelpers.AddQueryString(
            verificationUri,
            MultitenancyConstants.Identifier,
            multiTenantContextAccessor?.TenantInfo?.Id!);

        return verificationUri;
    }

    private static string BuildConfirmationEmailHtml(string userName, string confirmationUrl)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Confirm Your Email</title>
            </head>
            <body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f8fafc;">
                <table role="presentation" style="width: 100%; border-collapse: collapse;">
                    <tr>
                        <td align="center" style="padding: 40px 0;">
                            <table role="presentation" style="width: 100%; max-width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);">
                                <tr>
                                    <td style="padding: 40px 40px 30px 40px; text-align: center; background-color: #2563eb; border-radius: 8px 8px 0 0;">
                                        <h1 style="margin: 0; color: #ffffff; font-size: 24px; font-weight: 600;">Confirm Your Email Address</h1>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 40px;">
                                        <p style="margin: 0 0 20px 0; color: #334155; font-size: 16px; line-height: 1.6;">
                                            Hi {System.Net.WebUtility.HtmlEncode(userName)},
                                        </p>
                                        <p style="margin: 0 0 20px 0; color: #334155; font-size: 16px; line-height: 1.6;">
                                            Thank you for registering! Please confirm your email address by clicking the button below:
                                        </p>
                                        <table role="presentation" style="width: 100%; border-collapse: collapse;">
                                            <tr>
                                                <td align="center" style="padding: 30px 0;">
                                                    <a href="{System.Net.WebUtility.HtmlEncode(confirmationUrl)}" style="display: inline-block; padding: 14px 32px; background-color: #2563eb; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600; border-radius: 6px;">
                                                        Confirm Email Address
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                        <p style="margin: 0 0 20px 0; color: #64748b; font-size: 14px; line-height: 1.6;">
                                            If the button doesn't work, copy and paste this link into your browser:
                                        </p>
                                        <p style="margin: 0 0 20px 0; color: #2563eb; font-size: 14px; line-height: 1.6; word-break: break-all;">
                                            {System.Net.WebUtility.HtmlEncode(confirmationUrl)}
                                        </p>
                                        <p style="margin: 30px 0 0 0; color: #64748b; font-size: 14px; line-height: 1.6;">
                                            If you didn't create an account, you can safely ignore this email.
                                        </p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 20px 40px; background-color: #f1f5f9; border-radius: 0 0 8px 8px; text-align: center;">
                                        <p style="margin: 0; color: #94a3b8; font-size: 12px;">
                                            This is an automated message. Please do not reply to this email.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}