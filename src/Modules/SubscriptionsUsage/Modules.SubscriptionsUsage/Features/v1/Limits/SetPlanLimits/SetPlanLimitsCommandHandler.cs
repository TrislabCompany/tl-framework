using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;
using Trislab.Modules.SubscriptionsUsage.Data;
using Trislab.Modules.SubscriptionsUsage.Domain;
using Trislab.Modules.SubscriptionsUsage.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.SetPlanLimits;

public sealed class SetPlanLimitsCommandHandler(BillingDbContext dbContext)
    : ICommandHandler<SetPlanLimitsCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetPlanLimitsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plan = await dbContext.Plans.FirstOrDefaultAsync(p => p.Id == command.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Plan {command.PlanId} not found.")
                .Localized<BillingResources>("Billing.PlanNotFound", command.PlanId);

        plan.SetLimits(command.Limits.Select(l => new PlanLimit(l.MeterKey, l.Limit, l.ExtraUnitPrice)));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return plan.Id;
    }
}
