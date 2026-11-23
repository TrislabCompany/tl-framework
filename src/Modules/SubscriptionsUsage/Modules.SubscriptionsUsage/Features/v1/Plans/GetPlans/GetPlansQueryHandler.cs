using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Plans;
using Trislab.Modules.SubscriptionsUsage.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.GetPlans;

public sealed class GetPlansQueryHandler(BillingDbContext dbContext)
    : IQueryHandler<GetPlansQuery, IReadOnlyList<BillingPlanDto>>
{
    public async ValueTask<IReadOnlyList<BillingPlanDto>> Handle(GetPlansQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var plansQuery = dbContext.Plans.AsNoTracking();
        if (!query.IncludeInactive)
        {
            plansQuery = plansQuery.Where(p => p.IsActive);
        }

        var plans = await plansQuery.OrderBy(p => p.Key).ToListAsync(cancellationToken).ConfigureAwait(false);
        return plans
            .Select(p => new BillingPlanDto(p.Id, p.Key, p.Name, p.Currency, p.MonthlyBasePrice.Amount, p.OverageRates, p.IsActive, p.Interval, p.AnnualPrice?.Amount))
            .ToList();
    }
}
