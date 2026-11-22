using Trislab.Modules.Billing.Contracts.Dtos;
using Mediator;

namespace Trislab.Modules.Billing.Contracts.v1.Plans;

public sealed record GetPlansQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<BillingPlanDto>>;
