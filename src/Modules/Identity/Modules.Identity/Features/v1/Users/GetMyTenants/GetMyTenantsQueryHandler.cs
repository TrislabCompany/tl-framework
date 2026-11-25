using System.Collections.ObjectModel;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Users.GetMyTenants;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Users.GetMyTenants;

public sealed class GetMyTenantsQueryHandler : IQueryHandler<GetMyTenantsQuery, ReadOnlyCollection<TenantMembershipDto>>
{
    private readonly TenantSignInService _signIn;
    private readonly ICurrentUser _currentUser;

    public GetMyTenantsQueryHandler(TenantSignInService signIn, ICurrentUser currentUser)
    {
        _signIn = signIn;
        _currentUser = currentUser;
    }

    public async ValueTask<ReadOnlyCollection<TenantMembershipDto>> Handle(GetMyTenantsQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated())
        {
            throw new UnauthorizedException();
        }

        var tenants = await _signIn
            .GetMyTenantsAsync(_currentUser.GetUserId().ToString(), cancellationToken)
            .ConfigureAwait(false);
        return tenants.AsReadOnly();
    }
}
