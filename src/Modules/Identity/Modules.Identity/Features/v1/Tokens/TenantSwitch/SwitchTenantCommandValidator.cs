using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSwitch;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TenantSwitch;

public sealed class SwitchTenantCommandValidator : AbstractValidator<SwitchTenantCommand>
{
    public SwitchTenantCommandValidator()
    {
        RuleFor(p => p.Tenant).NotEmpty().MaximumLength(64);
    }
}
