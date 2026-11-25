using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Shared.Constants;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSwitch;
using Trislab.Modules.Identity.Localization;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TenantSwitch;

public sealed class SwitchTenantCommandHandler : ICommandHandler<SwitchTenantCommand, SignInResponse>
{
    private readonly TenantSignInService _signIn;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;

    public SwitchTenantCommandHandler(TenantSignInService signIn, ICurrentUser currentUser, IRequestContext requestContext)
    {
        _signIn = signIn;
        _currentUser = currentUser;
        _requestContext = requestContext;
    }

    public async ValueTask<SignInResponse> Handle(SwitchTenantCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        // An impersonation token belongs to the impersonated user's tenant only: end it first.
        if (_currentUser.GetUserClaims()?.Any(c => c.Type == ClaimConstants.ActorSubject) == true)
        {
            throw new CustomException(
                "end current impersonation before switching tenant",
                errors: null,
                System.Net.HttpStatusCode.BadRequest)
                .Localized<IdentityResources>("Identity.EndImpersonationFirst");
        }

        var caller = new SignInCaller(
            _requestContext.IpAddress ?? "unknown",
            _requestContext.UserAgent ?? "unknown",
            _requestContext.ClientId);

        return await _signIn
            .SwitchAsync(
                _currentUser.GetUserId().ToString(),
                _currentUser.GetTenant(),
                _currentUser.GetUserClaims()?.FirstOrDefault(c => c.Type == IdentityService.SecurityStampClaim)?.Value,
                _currentUser.GetUserClaims()?.FirstOrDefault(c => c.Type == IdentityService.SessionClaim)?.Value,
                request.Tenant.Trim(),
                request.SetDefault,
                request.App,
                caller,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
