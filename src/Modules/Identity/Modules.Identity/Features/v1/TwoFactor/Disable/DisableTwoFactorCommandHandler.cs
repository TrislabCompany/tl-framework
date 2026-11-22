using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Contracts.v1.TwoFactor;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Mediator;
using Microsoft.AspNetCore.Identity;

namespace Trislab.Modules.Identity.Features.v1.TwoFactor.Disable;

public sealed class DisableTwoFactorCommandHandler
    : ICommandHandler<DisableTwoFactorCommand, bool>
{
    private readonly UserManager<TrislabUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public DisableTwoFactorCommandHandler(UserManager<TrislabUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
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
        return true;
    }
}
