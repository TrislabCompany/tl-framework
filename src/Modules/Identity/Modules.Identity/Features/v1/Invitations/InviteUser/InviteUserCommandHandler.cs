using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Invitations.InviteUser;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Invitations.InviteUser;

public sealed class InviteUserCommandHandler(MembershipInvitationService invitations, ICurrentUser currentUser)
    : ICommandHandler<InviteUserCommand, InvitationDto>
{
    public async ValueTask<InvitationDto> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = currentUser.GetUserId();
        if (actor == Guid.Empty)
        {
            throw new UnauthorizedException();
        }

        return await invitations
            .InviteAsync(command.Email, command.FirstName, command.LastName, actor.ToString(), cancellationToken)
            .ConfigureAwait(false);
    }
}
