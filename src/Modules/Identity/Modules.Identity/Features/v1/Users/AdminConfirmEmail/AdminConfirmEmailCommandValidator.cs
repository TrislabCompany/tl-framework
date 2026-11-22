using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Users.AdminConfirmEmail;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.AdminConfirmEmail;

public sealed class AdminConfirmEmailCommandValidator : AbstractValidator<AdminConfirmEmailCommand>
{
    public AdminConfirmEmailCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);
    }
}
