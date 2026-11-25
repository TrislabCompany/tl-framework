using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Invitations.AcceptInvitation;

namespace Trislab.Modules.Identity.Features.v1.Invitations.AcceptInvitation;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).MaximumLength(256);
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
    }
}
