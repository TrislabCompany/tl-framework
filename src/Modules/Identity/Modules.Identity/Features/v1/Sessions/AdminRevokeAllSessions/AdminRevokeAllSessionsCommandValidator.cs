using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Sessions.AdminRevokeAllSessions;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Sessions.AdminRevokeAllSessions;

public sealed class AdminRevokeAllSessionsCommandValidator : AbstractValidator<AdminRevokeAllSessionsCommand>
{
    public AdminRevokeAllSessionsCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage(_ => localizer["Validation.ReasonMaxLength"])
            .When(x => x.Reason is not null);
    }
}