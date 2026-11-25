using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Users.ConfirmEmail;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.ConfirmEmail;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(_ => localizer["Validation.ConfirmationCodeRequired"]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => localizer["Validation.PasswordRequired"]);

        RuleFor(x => x.Tenant)
            .NotEmpty().WithMessage(_ => localizer["Validation.TenantRequired"]);
    }
}