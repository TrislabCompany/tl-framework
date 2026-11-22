using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Sessions.AdminRevokeSession;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Sessions.AdminRevokeSession;

public sealed class AdminRevokeSessionCommandValidator : AbstractValidator<AdminRevokeSessionCommand>
{
    public AdminRevokeSessionCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);

        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage(_ => localizer["Validation.SessionIdRequired"]);

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage(_ => localizer["Validation.ReasonMaxLength"])
            .When(x => x.Reason is not null);
    }
}