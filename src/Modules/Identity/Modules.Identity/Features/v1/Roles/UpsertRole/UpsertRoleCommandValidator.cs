using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Roles.UpsertRole;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Roles.UpsertRole;

public sealed class UpsertRoleCommandValidator : AbstractValidator<UpsertRoleCommand>
{
    public UpsertRoleCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(_ => localizer["Validation.RoleNameRequired"]);
    }
}