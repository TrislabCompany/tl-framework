using System.Collections.ObjectModel;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Persistence;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Mailing;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Web.Frontend;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trislab.Modules.Identity.Services;

/// <summary>How long an invitation link works.</summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Identity:Invitations";

    /// <summary>Hours until an invitation expires; 7 days by default.</summary>
    public int LifetimeHours { get; set; } = 168;
}

/// <summary>
/// Adds accounts to a tenant by invitation, the only way an administrator adds a user. The administrator
/// never sets or learns the account's password and never confirms its e-mail: the membership stays pending,
/// and invisible to the tenant, until the account's owner accepts the e-mailed link.
/// </summary>
/// <remarks>
/// <para>A new address gets an account without a password; its owner sets one when accepting, which also
/// confirms the e-mail (the link reached the mailbox). An existing account keeps its password, and its owner
/// accepts while signed in to it.</para>
/// <para>The token is 256 random bits, e-mailed once and stored only as its SHA-256. It expires, can be revoked,
/// and is consumed with a conditional update, so it works once.</para>
/// </remarks>
public sealed class MembershipInvitationService(
    UserManager<TrislabUser> userManager,
    IdentityDbContext db,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    IMultiTenantStore<AppTenantInfo> tenantStore,
    IEventTenantScope tenantScope,
    IServiceScopeFactory scopeFactory,
    IBackgroundMailer mailer,
    IFrontendOriginResolver originResolver,
    IOptions<InvitationOptions> options,
    IdentityAuditor auditor,
    IOptions<TenantGraceOptions> grace,
    TimeProvider timeProvider,
    IOptions<SelfRegistrationOptions> responseOptions,
    ILogger<MembershipInvitationService> logger)
{
    /// <summary>
    /// Invites an address to the current tenant. The answer, and its least duration, are the same whether the
    /// address has an account or not: creating an account for a new address takes longer, and the timing must
    /// not tell an administrator which addresses have accounts elsewhere.
    /// </summary>
    public async Task<InvitationDto> InviteAsync(
        string email, string? firstName, string? lastName, string invitedBy, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(invitedBy);

        var started = timeProvider.GetTimestamp();
        try
        {
            return await InviteCoreAsync(email, firstName, lastName, invitedBy, ct).ConfigureAwait(false);
        }
        finally
        {
            await ResponseTime.PadAsync(timeProvider, started, responseOptions.Value.MinimumResponseTime, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<InvitationDto> InviteCoreAsync(
        string email, string? firstName, string? lastName, string invitedBy, CancellationToken ct)
    {
        var tenantId = CurrentTenantId();
        email = email.Trim();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var account = await userManager.FindByEmailAsync(userManager.NormalizeEmail(email)).ConfigureAwait(false)
            ?? await CreateAccountWithoutPasswordAsync(email, firstName, lastName).ConfigureAwait(false);

        var membership = await db.TenantMemberships
            .FirstOrDefaultAsync(m => m.UserId == account.Id, ct)
            .ConfigureAwait(false);

        if (membership is { IsPending: false })
        {
            // The administrator sees the tenant's members already, so this says nothing new.
            throw new CustomException("This user is already a member.", errors: null, HttpStatusCode.Conflict)
                .Localized<IdentityResources>("Identity.AlreadyAMember");
        }

        if (membership is null)
        {
            membership = TenantMembership.CreatePending(account.Id, tenantId, invitedBy);
            db.TenantMemberships.Add(membership);
        }
        else
        {
            // A new invitation replaces the old ones of the same pending membership.
            var previous = await db.MembershipInvitations
                .Where(i => i.MembershipId == membership.Id && i.AcceptedOnUtc == null && i.RevokedOnUtc == null)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var old in previous)
            {
                old.Revoke(invitedBy, now);
            }
        }

        var token = NewToken();
        var invitation = MembershipInvitation.Create(
            membership.Id, account.Id, email, HashToken(token), invitedBy, now, TimeSpan.FromHours(options.Value.LifetimeHours));
        db.MembershipInvitations.Add(invitation);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await SendInvitationAsync(email, token, tenantId, needsPassword: NeedsPassword(account)).ConfigureAwait(false);

        await auditor.WriteAsync(
            SecurityAction.InvitationSent,
            account.Id,
            "InvitationSent",
            new Dictionary<string, object?> { ["invitationId"] = invitation.Id, ["invitedBy"] = invitedBy, ["tenant"] = tenantId },
            ct: ct).ConfigureAwait(false);

        return ToDto(invitation, now);
    }

    /// <summary>A page of the current tenant's invitations, newest first.</summary>
    public async Task<PagedResponse<InvitationDto>> ListAsync(IPagedQuery query, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var page = await db.MembershipInvitations
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedOnUtc)
            .ToPagedResponseAsync(query, ct)
            .ConfigureAwait(false);

        return new PagedResponse<InvitationDto>
        {
            Items = page.Items.Select(i => ToDto(i, now)).ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    /// <summary>
    /// Revokes an invitation of the current tenant. A membership nobody accepted goes with it. Both steps are
    /// conditional, so a revoke racing an accept leaves one consistent outcome: whichever ran first wins.
    /// </summary>
    public async Task RevokeAsync(Guid invitationId, string revokedBy, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var invitation = await db.MembershipInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("invitation not found").Localized<IdentityResources>("Identity.InvitationNotFound");

        var revoked = await db.MembershipInvitations
            .Where(i => i.Id == invitationId && i.AcceptedOnUtc == null && i.RevokedOnUtc == null)
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.RevokedOnUtc, now).SetProperty(i => i.RevokedBy, revokedBy), ct)
            .ConfigureAwait(false);
        if (revoked == 0)
        {
            return;
        }

        await db.TenantMemberships
            .Where(m => m.Id == invitation.MembershipId && m.AcceptedOnUtc == null)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        await DeleteUnclaimedAccountAsync(invitation.UserId, ct).ConfigureAwait(false);

        await auditor.WriteAsync(
            SecurityAction.InvitationRevoked,
            invitation.UserId,
            "InvitationRevoked",
            new Dictionary<string, object?> { ["invitationId"] = invitation.Id, ["revokedBy"] = revokedBy },
            ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes an account that was created for an invitation nobody accepted: no password, no confirmed e-mail, no
    /// membership in any tenant and no open invitation. Nothing else could have used it, and leaving it would keep
    /// the address "taken" with nobody behind it.
    /// </summary>
    private async Task DeleteUnclaimedAccountAsync(string userId, CancellationToken ct)
    {
        var account = await db.FindAccountAsync(userId, ct).ConfigureAwait(false);
        if (account is not { PasswordHash: null, EmailConfirmed: false })
        {
            return;
        }

        var inUse = await db.TenantMemberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == userId, ct).ConfigureAwait(false)
            || await db.MembershipInvitations.IgnoreQueryFilters()
                .AnyAsync(i => i.UserId == userId && i.AcceptedOnUtc == null && i.RevokedOnUtc == null, ct).ConfigureAwait(false);
        if (inUse)
        {
            return;
        }

        var deleted = await userManager.DeleteAsync(account).ConfigureAwait(false);
        if (!deleted.Succeeded)
        {
            logger.LogWarning("An unclaimed account {UserId} could not be deleted after its invitation was revoked", userId);
        }
    }

    /// <summary>
    /// Accepts an invitation, in one transaction. An account its owner has not proven yet (no password, or an
    /// unconfirmed e-mail) gets <paramref name="password"/> here, which replaces any password someone else may have
    /// set before the owner confirmed the address. A confirmed account with a password must be the signed-in caller
    /// (<paramref name="callerUserId"/>), and its password never changes here.
    /// </summary>
    public async Task<AcceptedInvitation> AcceptAsync(
        string token, string? password, string? firstName, string? lastName, string? callerUserId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var hash = HashToken(token);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // The owner may be signed in to another tenant or not at all: find the invitation by its hash across tenants.
        var found = await db.MembershipInvitations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.TokenHash == hash)
            .Select(i => new { Invitation = i, TenantId = EF.Property<string>(i, "TenantId") })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (found is null)
        {
            throw InvalidInvitation();
        }

        var invitation = found.Invitation;
        var tenantId = found.TenantId;
        var status = invitation.StatusAt(now);
        if (status != InvitationStatus.Pending)
        {
            if (status == InvitationStatus.Expired)
            {
                await tenantScope.ExecuteAsync(tenantId, async token2 =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IdentityAuditor>().WriteAsync(
                        SecurityAction.InvitationExpired,
                        invitation.UserId,
                        "InvitationExpired",
                        new Dictionary<string, object?> { ["invitationId"] = invitation.Id },
                        AuditSeverity.Warning,
                        token2).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);
            }

            throw InvalidInvitation();
        }

        // A deactivated or lapsed tenant takes no new members.
        var tenant = await tenantStore.GetAsync(tenantId).WaitAsync(ct).ConfigureAwait(false);
        if (!IsOpen(tenant, tenantId, now))
        {
            throw InvalidInvitation();
        }

        await tenantScope.ExecuteAsync(tenantId, async scopedCt =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var scopedDb = services.GetRequiredService<IdentityDbContext>();
            var scopedUsers = services.GetRequiredService<UserManager<TrislabUser>>();

            var account = await scopedDb.Users
                .IgnoreQueryFilters([IdentityDbContext.MembershipFilter])
                .FirstOrDefaultAsync(u => u.Id == invitation.UserId, scopedCt)
                .ConfigureAwait(false)
                ?? throw InvalidInvitation();

            var settingPassword = NeedsPassword(account);
            if (!settingPassword)
            {
                // An existing account: only its signed-in owner accepts, and its password is not touched.
                if (string.IsNullOrEmpty(callerUserId))
                {
                    throw new UnauthorizedException("sign in to the invited account to accept")
                        .Localized<IdentityResources>("Identity.InvitationSignInRequired");
                }

                if (!string.Equals(callerUserId, account.Id, StringComparison.Ordinal))
                {
                    throw new ForbiddenException("this invitation is for another account")
                        .Localized<IdentityResources>("Identity.InvitationForAnotherAccount");
                }
            }
            else if (string.IsNullOrWhiteSpace(password))
            {
                throw new CustomException("choose a password to accept", errors: null, HttpStatusCode.BadRequest)
                    .Localized<IdentityResources>("Identity.InvitationPasswordRequired");
            }

            await MembershipRules.EnsureOperatorsStayInRootAsync(scopedDb, account.Id, tenantId, scopedCt).ConfigureAwait(false);

            // One transaction: the token, the password, the e-mail confirmation and the membership change together
            // or not at all, so a failure never leaves a used token without a membership.
            var strategy = scopedDb.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await scopedDb.Database.BeginTransactionAsync(scopedCt).ConfigureAwait(false);

                // Consume the token atomically: of two concurrent accepts, one wins.
                var consumed = await scopedDb.MembershipInvitations
                    .Where(i => i.Id == invitation.Id && i.AcceptedOnUtc == null && i.RevokedOnUtc == null && i.ExpiresOnUtc > now)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.AcceptedOnUtc, now), scopedCt)
                    .ConfigureAwait(false);
                if (consumed == 0)
                {
                    throw InvalidInvitation();
                }

                if (settingPassword)
                {
                    if (account.PasswordHash is not null)
                    {
                        // Someone set a password before the owner proved the address (a sign-up with another person's
                        // e-mail). What they joined on their own goes with it; what an administrator offered stays.
                        await scopedDb.TenantMemberships
                            .IgnoreQueryFilters()
                            .Where(m => m.UserId == account.Id && m.InvitedBy == null && m.Id != invitation.MembershipId)
                            .ExecuteDeleteAsync(scopedCt)
                            .ConfigureAwait(false);
                    }

                    await SetOwnersPasswordAsync(scopedUsers, account, password!, firstName, lastName).ConfigureAwait(false);
                }

                var membership = await scopedDb.TenantMemberships
                    .FirstOrDefaultAsync(m => m.Id == invitation.MembershipId, scopedCt)
                    .ConfigureAwait(false)
                    ?? throw InvalidInvitation();
                membership.Accept(now);
                await scopedDb.SaveChangesAsync(scopedCt).ConfigureAwait(false);

                await transaction.CommitAsync(scopedCt).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await MembershipRules.AssignDefaultRoleAndGroupsAsync(scopedUsers, scopedDb, account, "Invitation", scopedCt).ConfigureAwait(false);
            await services.GetRequiredService<IUserPermissionService>().InvalidatePermissionCacheAsync(account.Id, scopedCt).ConfigureAwait(false);

            var scopedAuditor = services.GetRequiredService<IdentityAuditor>();
            var data = new Dictionary<string, object?> { ["invitationId"] = invitation.Id, ["tenant"] = tenantId, ["newAccount"] = settingPassword };
            await scopedAuditor.WriteAsync(SecurityAction.InvitationAccepted, account.Id, "InvitationAccepted", data, ct: scopedCt).ConfigureAwait(false);
            await scopedAuditor.WriteAsync(SecurityAction.MembershipAdded, account.Id, "MembershipAdded", data, ct: scopedCt).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return new AcceptedInvitation(tenantId, tenant?.Name);
    }

    /// <summary>
    /// Offers the tenant's first administrator a membership when the address already has an account: used by
    /// seeding, where no administrator exists yet to invite. 
    /// </summary>
    internal async Task InviteExistingAccountAsync(TrislabUser account, string tenantId, string invitedBy, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var membership = TenantMembership.CreatePending(account.Id, tenantId, invitedBy);
        db.TenantMemberships.Add(membership);
        var token = NewToken();
        db.MembershipInvitations.Add(MembershipInvitation.Create(
            membership.Id, account.Id, account.Email!, HashToken(token), invitedBy, now, TimeSpan.FromHours(options.Value.LifetimeHours)));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await SendInvitationAsync(account.Email!, token, tenantId, needsPassword: NeedsPassword(account)).ConfigureAwait(false);
        await auditor.WriteAsync(
            SecurityAction.InvitationSent, account.Id, "InvitationSent",
            new Dictionary<string, object?> { ["invitedBy"] = invitedBy, ["tenant"] = tenantId }, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// True when the account's owner has not proven it yet: no password, or an e-mail nobody confirmed (anyone can
    /// sign up with an address they don't own). Accepting such an invitation sets the password from the link.
    /// </summary>
    internal static bool NeedsPassword(TrislabUser account) => account.PasswordHash is null || !account.EmailConfirmed;

    private bool IsOpen(AppTenantInfo? tenant, string tenantId, DateTime now)
    {
        if (string.Equals(tenantId, MultitenancyConstants.Root.Id, StringComparison.Ordinal))
        {
            return true;
        }

        return tenant is { IsActive: true } && now <= tenant.ValidUpto.AddDays(grace.Value.GracePeriodDays);
    }

    private static async Task SetOwnersPasswordAsync(
        UserManager<TrislabUser> users, TrislabUser account, string password, string? firstName, string? lastName)
    {
        // Check the policy first: a weak password must fail before anything changes (the transaction rolls back).
        foreach (var validator in users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(users, account, password).ConfigureAwait(false);
            if (!check.Succeeded)
            {
                throw PasswordRejected(check);
            }
        }

        if (account.PasswordHash is not null)
        {
            // A password set before the owner proved the address is not the owner's: it goes.
            EnsureSucceeded(await users.RemovePasswordAsync(account).ConfigureAwait(false));
        }

        EnsureSucceeded(await users.AddPasswordAsync(account, password).ConfigureAwait(false));

        // The link reached the mailbox, which is what confirming the e-mail proves.
        account.EmailConfirmed = true;
        if (!string.IsNullOrWhiteSpace(firstName)) account.FirstName = firstName.Trim();
        if (!string.IsNullOrWhiteSpace(lastName)) account.LastName = lastName.Trim();
        EnsureSucceeded(await users.UpdateAsync(account).ConfigureAwait(false));
        EnsureSucceeded(await users.UpdateSecurityStampAsync(account).ConfigureAwait(false));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw PasswordRejected(result);
        }
    }

    private static CustomException PasswordRejected(IdentityResult result) =>
        new CustomException(
            "The password does not meet the policy.",
            result.Errors.Select(e => e.Description).ToList(),
            HttpStatusCode.BadRequest)
            .Localized<IdentityResources>("Identity.UnableToRegisterUser");

    /// <summary>SHA-256 of an invitation token, as stored.</summary>
    internal static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private async Task<TrislabUser> CreateAccountWithoutPasswordAsync(string email, string? firstName, string? lastName)
    {
        var account = new TrislabUser
        {
            Email = email,
            UserName = $"user-{Guid.NewGuid():N}"[..17],
            FirstName = firstName?.Trim(),
            LastName = lastName?.Trim(),
            EmailConfirmed = false,
        };

        var result = await userManager.CreateAsync(account).ConfigureAwait(false);
        if (!result.Succeeded && result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            // Another request created the account a moment ago: invite that one, as if it had been there.
            return await userManager.FindByEmailAsync(userManager.NormalizeEmail(email)).ConfigureAwait(false)
                ?? throw new CustomException("Unable to invite the user.", errors: null, HttpStatusCode.Conflict)
                    .Localized<IdentityResources>("Identity.UnableToRegisterUser");
        }

        if (!result.Succeeded)
        {
            throw new CustomException(
                "Unable to invite the user.",
                result.Errors.Select(e => e.Description).ToList(),
                HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.UnableToRegisterUser");
        }

        return account;
    }

    private async Task SendInvitationAsync(string email, string token, string tenantId, bool needsPassword)
    {
        var tenant = await tenantStore.GetAsync(tenantId).ConfigureAwait(false);
        var tenantName = tenant?.Name ?? tenantId;
        var link = QueryHelpers.AddQueryString(
            $"{originResolver.ResolveDefault().TrimEnd('/')}/accept-invitation",
            new Dictionary<string, string?> { ["token"] = token, ["new"] = needsPassword ? "1" : "0" });

        var intro = needsPassword
            ? $"You are invited to join {tenantName}. Open the link to choose your password and accept."
            : $"You are invited to join {tenantName}. Sign in with your account, then open the link to accept.";

        var mail = new MailRequest(
            new Collection<string> { email },
            $"Invitation to {tenantName}",
            HtmlEmail.LinkAction(
                heading: $"Join {tenantName}",
                intro: intro,
                actionUrl: link,
                actionLabel: "Accept the invitation"),
            textBody: $"{intro} {link}");

        mailer.Enqueue(mail);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Invitation e-mail queued for tenant {TenantId}", tenantId);
        }
    }

    private string CurrentTenantId() =>
        tenantAccessor.MultiTenantContext?.TenantInfo?.Id is { Length: > 0 } id
            ? id
            : throw new UnauthorizedException("invalid tenant").Localized("Error.InvalidTenant");

    private static InvitationDto ToDto(MembershipInvitation i, DateTime now) =>
        new(i.Id, i.Email, i.StatusAt(now).ToString(), i.CreatedOnUtc, i.ExpiresOnUtc, i.AcceptedOnUtc, i.RevokedOnUtc);

    private static CustomException InvalidInvitation() =>
        new CustomException("The invitation is invalid, expired or already used.", errors: null, HttpStatusCode.BadRequest)
            .Localized<IdentityResources>("Identity.InvitationInvalid");
}

/// <summary>The tenant an accepted invitation opened.</summary>
public sealed record AcceptedInvitation(string Tenant, string? TenantName);
