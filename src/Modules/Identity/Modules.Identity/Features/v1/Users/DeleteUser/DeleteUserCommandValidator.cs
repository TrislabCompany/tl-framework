using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Users.DeleteUser;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.DeleteUser;

public sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(_ => localizer["Validation.UserIdRequired"]);
    }
}