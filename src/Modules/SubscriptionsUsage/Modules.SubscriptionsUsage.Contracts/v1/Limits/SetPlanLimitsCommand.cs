using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;

/// <summary>Replaces a plan's limits (root). A meter left out falls back to its default limit.</summary>
public sealed record SetPlanLimitsCommand(Guid PlanId, IReadOnlyList<PlanLimitDto> Limits) : ICommand<Guid>;
