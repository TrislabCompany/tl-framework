using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Contracts.v1.TwoFactor;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Services;
using Mediator;
using Microsoft.AspNetCore.Identity;

namespace Trislab.Modules.Identity.Features.v1.TwoFactor.VerifyEnroll;

public sealed class VerifyEnrollTwoFactorCommandHandler
    : ICommandHandler<VerifyEnrollTwoFactorCommand, bool>
{
    private readonly UserManager<TrislabUser> _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly IdentityAuditor _auditor;

    public VerifyEnrollTwoFactorCommandHandler(UserManager<TrislabUser> userManager, ICurrentUser currentUser, IdentityAuditor auditor)
    {
        _userManager = userManager;
        _currentUser = currentUser;
        _auditor = auditor;
    }

    public async ValueTask<bool> Handle(
        VerifyEnrollTwoFactorCommand command, CancellationToken cancellationToken)
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

        var sanitized = command.Code.Replace(" ", string.Empty, StringComparison.Ordinal);
        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            sanitized);

        if (!valid)
        {
            throw new CustomException(
                "The authenticator code is invalid.",
                errors: null,
                System.Net.HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.AuthenticatorCodeInvalid");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        // The change rotated the security stamp, so the account's sessions end at their next refresh.
        await _auditor.WriteAsync(SecurityAction.TwoFactorEnabled, user.Id, "TwoFactorEnabled", ct: cancellationToken).ConfigureAwait(false);
        return true;
    }
}
