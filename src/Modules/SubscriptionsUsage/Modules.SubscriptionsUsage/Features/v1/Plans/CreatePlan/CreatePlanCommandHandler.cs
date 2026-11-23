using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Plans;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Domain;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.CreatePlan;

public sealed class CreatePlanCommandHandler(BillingDbContext dbContext)
    : ICommandHandler<CreatePlanCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = BillingPlan.Create(command.Key, command.Name, command.Currency, command.MonthlyBasePrice,
            command.OverageRates, command.Interval, command.AnnualPrice);
        dbContext.Plans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return plan.Id;
    }
}
