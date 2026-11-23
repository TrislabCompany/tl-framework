using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Plans;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.UpdatePlan;

public sealed class UpdatePlanCommandHandler(BillingDbContext dbContext)
    : ICommandHandler<UpdatePlanCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdatePlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = await dbContext.Plans.FirstOrDefaultAsync(p => p.Id == command.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Plan {command.PlanId} not found.")
                .Localized<BillingResources>("Billing.PlanNotFound", command.PlanId);

        plan.Update(command.Name, command.MonthlyBasePrice, command.OverageRates, command.Interval, command.AnnualPrice);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return plan.Id;
    }
}
