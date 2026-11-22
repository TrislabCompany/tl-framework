using FluentValidation;
using Trislab.Modules.Auditing.Contracts.v1.GetExceptionAudits;
using Trislab.Modules.Auditing.Localization;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Auditing.Features.v1.GetExceptionAudits;

public sealed class GetExceptionAuditsQueryValidator : AbstractValidator<GetExceptionAuditsQuery>
{
    public GetExceptionAuditsQueryValidator(IStringLocalizer<AuditingResources> localizer)
    {
        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage(_ => localizer["Validation.DateRangeOrder"]);
    }
}
