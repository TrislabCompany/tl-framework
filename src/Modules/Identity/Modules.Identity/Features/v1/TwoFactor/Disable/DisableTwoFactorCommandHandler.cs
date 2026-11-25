using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Contracts.v1.TwoFactor;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Services;
using Mediator;
using Microsoft.AspNetCore.Identity;

namespace Trislab.Modules.Identity.Features.v1.TwoFactor.Disable;

public sealed class DisableTwoFactorCommandHandler
    : ICommandHandler<DisableTwoFactorCommand, bool>
{
    private readonly UserManager<TrislabUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly IdentityAuditor _auditor;

    public DisableTwoFactorCommandHandler(UserManager<TrislabUser> userManager, ICurrentUser currentUser, IdentityAuditor auditor)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _auditor = auditor;
    }

    public async ValueTask<bool> Handle(
        DisableTwoFactorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var userId = _currentUser.GetUserId().ToString();
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User {userId} not found.")
                .Localized<IdentityResources>("Identity.UserNotFoundById", userId);

        // Require current password so a stolen access token alone can't downgrade
        // account security.
        if (!await _userManager.CheckPasswordAsync(user, command.CurrentPassword))
        {
            throw new UnauthorizedException("Current password is incorrect.")
                .Localized<IdentityResources>("Identity.CurrentPasswordIncorrect");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        // The change rotated the security stamp, so the account's sessions end at their next refresh.
        await _auditor.WriteAsync(SecurityAction.TwoFactorDisabled, user.Id, "TwoFactorDisabled", ct: cancellationToken).ConfigureAwait(false);
        return true;
    }
}
