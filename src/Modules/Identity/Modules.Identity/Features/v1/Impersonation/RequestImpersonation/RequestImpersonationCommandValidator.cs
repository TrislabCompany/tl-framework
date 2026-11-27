using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Impersonation.RequestImpersonation;

namespace Trislab.Modules.Identity.Features.v1.Impersonation.RequestImpersonation;

public sealed class RequestImpersonationCommandValidator : AbstractValidator<RequestImpersonationCommand>
{
    public RequestImpersonationCommandValidator()
    {
        RuleFor(p => p.TargetUserId).NotEmpty();
        RuleFor(p => p.TargetTenantId).NotEmpty();

        // The user reads the reason before answering, so it is required.
        RuleFor(p => p.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(500);
    }
}
