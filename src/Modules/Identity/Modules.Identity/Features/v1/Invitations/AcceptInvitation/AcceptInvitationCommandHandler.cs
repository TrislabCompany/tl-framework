using Trislab.Modules.Identity.Contracts.v1.Invitations.AcceptInvitation;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Invitations.AcceptInvitation;

public sealed class AcceptInvitationCommandHandler(MembershipInvitationService invitations)
    : ICommandHandler<AcceptInvitationCommand, AcceptInvitationResponse>
{
    public async ValueTask<AcceptInvitationResponse> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var accepted = await invitations
            .AcceptAsync(command.Token, command.Password, command.FirstName, command.LastName, command.CallerUserId, cancellationToken)
            .ConfigureAwait(false);
        return new AcceptInvitationResponse(accepted.Tenant, accepted.TenantName);
    }
}
