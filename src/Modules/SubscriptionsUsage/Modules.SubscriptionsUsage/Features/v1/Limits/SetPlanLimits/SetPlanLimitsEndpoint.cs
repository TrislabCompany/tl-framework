using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.SetPlanLimits;

public static class SetPlanLimitsEndpoint
{
    internal static RouteHandlerBuilder MapSetPlanLimitsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/plans/{planId:guid}/limits",
                async (Guid planId, SetPlanLimitsCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = body with { PlanId = planId };
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("SetPlanLimits")
            .WithSummary("Replace a plan's limits per usage meter")
            .RequirePermission(BillingPermissions.Manage);
    }
}
