using FluentValidation;
using Trislab.Modules.SubscriptionsUsage.Contracts.Limits;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.SetPlanLimits;

/// <summary>Every meter key must be one the product declared, once; limits and prices are never negative.</summary>
public sealed class SetPlanLimitsCommandValidator : AbstractValidator<SetPlanLimitsCommand>
{
    public SetPlanLimitsCommandValidator(IEnumerable<IUsageMeter> meters)
    {
        var declared = meters.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);

        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Limits).NotNull();
        RuleFor(x => x.Limits)
            .Must(limits => limits.Select(l => l.MeterKey).Distinct(StringComparer.Ordinal).Count() == limits.Count)
            .When(x => x.Limits is not null)
            .WithMessage("A meter can have one limit per plan.");
        RuleForEach(x => x.Limits).ChildRules(limit =>
        {
            limit.RuleFor(l => l.MeterKey)
                .NotEmpty()
                .Must(declared.Contains)
                .WithMessage("'{PropertyValue}' is not a usage meter of this product.");
            limit.RuleFor(l => l.Limit).GreaterThanOrEqualTo(0).When(l => l.Limit.HasValue);
            limit.RuleFor(l => l.ExtraUnitPrice).GreaterThanOrEqualTo(0).When(l => l.ExtraUnitPrice.HasValue);
        });
    }
}
