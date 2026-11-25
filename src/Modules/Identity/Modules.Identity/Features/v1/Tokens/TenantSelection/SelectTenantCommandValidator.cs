using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSelection;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TenantSelection;

public sealed class SelectTenantCommandValidator : AbstractValidator<SelectTenantCommand>
{
    public SelectTenantCommandValidator()
    {
        RuleFor(p => p.SelectionToken).NotEmpty().MaximumLength(4096);
        RuleFor(p => p.Tenant).NotEmpty().MaximumLength(64);
    }
}
