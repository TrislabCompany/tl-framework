using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Tenants.RestoreAdminAccess;

namespace Trislab.Modules.Identity.Features.v1.Tenants.RestoreAdminAccess;

public sealed class RestoreAdminAccessCommandValidator : AbstractValidator<RestoreAdminAccessCommand>
{
    public RestoreAdminAccessCommandValidator()
    {
        RuleFor(p => p.TenantId).NotEmpty();
        RuleFor(p => p.Email).Cascade(CascadeMode.Stop).NotEmpty().EmailAddress().MaximumLength(256);

        // The tenant's Admins read the reason in their e-mail and audit trail.
        RuleFor(p => p.Reason).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(500);
        RuleFor(p => p.FirstName).MaximumLength(100);
        RuleFor(p => p.LastName).MaximumLength(100);
    }
}
