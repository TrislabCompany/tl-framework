using Trislab.Framework.Core.Context;
using Trislab.Modules.Identity.Contracts.v1.Invitations.RevokeInvitation;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Invitations.RevokeInvitation;

public sealed class RevokeInvitationCommandHandler(MembershipInvitationService invitations, ICurrentUser currentUser)
    : ICommandHandler<RevokeInvitationCommand, Unit>
{
    public async ValueTask<Unit> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await invitations
            .RevokeAsync(command.InvitationId, currentUser.GetUserId().ToString(), cancellationToken)
            .ConfigureAwait(false);
        return Unit.Value;
    }
}
