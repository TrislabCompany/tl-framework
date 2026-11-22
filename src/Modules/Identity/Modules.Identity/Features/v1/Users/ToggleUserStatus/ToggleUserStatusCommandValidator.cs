using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Users.ToggleUserStatus;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.ToggleUserStatus;

public sealed class ToggleUserStatusCommandValidator : AbstractValidator<ToggleUserStatusCommand>
{
    public ToggleUserStatusCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);
    }
}