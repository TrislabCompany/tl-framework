using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.TwoFactor;

namespace Trislab.Modules.Identity.Features.v1.TwoFactor.Disable;

public sealed class DisableTwoFactorCommandValidator : AbstractValidator<DisableTwoFactorCommand>
{
    public DisableTwoFactorCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}
