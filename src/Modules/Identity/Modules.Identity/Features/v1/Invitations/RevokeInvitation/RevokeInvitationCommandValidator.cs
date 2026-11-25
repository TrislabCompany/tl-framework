using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Invitations.RevokeInvitation;

namespace Trislab.Modules.Identity.Features.v1.Invitations.RevokeInvitation;

public sealed class RevokeInvitationCommandValidator : AbstractValidator<RevokeInvitationCommand>
{
    public RevokeInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId).NotEmpty();
    }
}
