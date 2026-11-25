using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Invitations.GetInvitations;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Invitations.GetInvitations;

public sealed class GetInvitationsQueryHandler(MembershipInvitationService invitations)
    : IQueryHandler<GetInvitationsQuery, PagedResponse<InvitationDto>>
{
    public async ValueTask<PagedResponse<InvitationDto>> Handle(GetInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await invitations.ListAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
