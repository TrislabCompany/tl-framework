using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Groups.RemoveUserFromGroup;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Groups.RemoveUserFromGroup;

public sealed class RemoveUserFromGroupCommandValidator : AbstractValidator<RemoveUserFromGroupCommand>
{
    public RemoveUserFromGroupCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage(_ => localizer["Validation.GroupIdRequired"]);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);
    }
}