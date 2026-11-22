using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Modules.Identity.Contracts.v1.Sessions.GetTenantSessions;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Sessions.GetTenantSessions;

public sealed class GetTenantSessionsValidator : AbstractValidator<GetTenantSessionsQuery>
{
    public GetTenantSessionsValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage(_ => localizer["Validation.PageNumberMinimum"]);

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(_ => localizer["Validation.PageSizeMinimum"]);
    }
}
