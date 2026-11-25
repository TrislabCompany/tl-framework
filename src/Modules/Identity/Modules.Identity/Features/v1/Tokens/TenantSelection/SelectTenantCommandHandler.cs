using Trislab.Framework.Core.Context;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSelection;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TenantSelection;

public sealed class SelectTenantCommandHandler : ICommandHandler<SelectTenantCommand, SignInResponse>
{
    private readonly TenantSignInService _signIn;
    private readonly IRequestContext _requestContext;

    public SelectTenantCommandHandler(TenantSignInService signIn, IRequestContext requestContext)
    {
        _signIn = signIn;
        _requestContext = requestContext;
    }

    public async ValueTask<SignInResponse> Handle(SelectTenantCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = new SignInCaller(
            _requestContext.IpAddress ?? "unknown",
            _requestContext.UserAgent ?? "unknown",
            _requestContext.ClientId);

        return await _signIn
            .SelectAsync(request.SelectionToken, request.Tenant.Trim(), request.SetDefault, request.App, caller, cancellationToken)
            .ConfigureAwait(false);
    }
}
