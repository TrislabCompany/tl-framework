using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Framework.Web.Validation;
using Trislab.Modules.Auditing.Contracts.v1.GetMyAudits;
using Trislab.Modules.Auditing.Features.v1.GetAudits;
using Trislab.Modules.Auditing.Localization;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Auditing.Features.v1.GetMyAudits;

public sealed class GetMyAuditsQueryValidator : AbstractValidator<GetMyAuditsQuery>
{
    public GetMyAuditsQueryValidator(
        IStringLocalizer<SharedResources> localizer,
        IStringLocalizer<AuditingResources> auditLocalizer)
    {
        Include(new PagedQueryValidator<GetMyAuditsQuery>(localizer));

        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage(_ => auditLocalizer["Validation.DateRangeOrder"]);

        RuleFor(q => q)
            .Must(q =>
                !q.FromUtc.HasValue
                || !q.ToUtc.HasValue
                || (q.ToUtc.Value - q.FromUtc.Value) <= GetAuditsQueryHandler.MaxWindow)
            .WithMessage(_ => auditLocalizer["Validation.WindowExceeded", GetAuditsQueryHandler.MaxWindowDays]);
    }
}
