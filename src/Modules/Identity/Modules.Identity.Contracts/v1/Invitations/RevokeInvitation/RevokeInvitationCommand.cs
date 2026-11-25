using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Invitations.RevokeInvitation;

/// <summary>Revokes an invitation; a membership nobody accepted goes with it.</summary>
public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand<Unit>;
