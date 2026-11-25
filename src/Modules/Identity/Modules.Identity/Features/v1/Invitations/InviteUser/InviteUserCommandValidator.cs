using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Invitations.InviteUser;

namespace Trislab.Modules.Identity.Features.v1.Invitations.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
    }
}
