using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Roles.UpdatePermissions;

namespace Trislab.Modules.Identity.Features.v1.Roles.UpdateRolePermissions;

public sealed class UpdatePermissionsCommandValidator : AbstractValidator<UpdatePermissionsCommand>
{
    public UpdatePermissionsCommandValidator()
    {
        RuleFor(r => r.RoleId)
            .NotEmpty();
        RuleFor(r => r.Permissions)
            .NotNull();
    }
}