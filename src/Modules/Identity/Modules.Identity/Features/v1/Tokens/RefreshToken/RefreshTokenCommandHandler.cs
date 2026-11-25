using Trislab.Framework.Core.Context;
using Trislab.Modules.Identity.Contracts.v1.Tokens.RefreshToken;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Tokens.RefreshToken;

public sealed class RefreshTokenCommandHandler
    : ICommandHandler<RefreshTokenCommand, RefreshTokenCommandResponse>
{
    private readonly TenantSignInService _signIn;
    private readonly IRequestContext _requestContext;

    public RefreshTokenCommandHandler(TenantSignInService signIn, IRequestContext requestContext)
    {
        _signIn = signIn;
        _requestContext = requestContext;
    }

    public async ValueTask<RefreshTokenCommandResponse> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The session that holds the refresh token names the user and the tenant, so no tenant header is needed.
        var token = await _signIn
            .RefreshAsync(request.RefreshToken, request.Token, request.App, _requestContext.ClientId, cancellationToken)
            .ConfigureAwait(false);

        return new RefreshTokenCommandResponse(
            Token: token.AccessToken,
            RefreshToken: token.RefreshToken,
            RefreshTokenExpiryTime: token.RefreshTokenExpiresAt);
    }
}
