using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Sessions.RevokeSession;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Sessions.RevokeSession;

public sealed class RevokeSessionCommandValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage(_ => localizer["Validation.SessionIdRequired"]);
    }
}