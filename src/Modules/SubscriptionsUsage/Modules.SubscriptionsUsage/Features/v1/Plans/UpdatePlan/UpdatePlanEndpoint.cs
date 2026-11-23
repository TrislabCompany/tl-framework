using Trislab.Modules.SubscriptionsUsage.Contracts.Authorization;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Plans;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.UpdatePlan;

public static class UpdatePlanEndpoint
{
    internal static RouteHandlerBuilder MapUpdatePlanEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/plans/{planId:guid}",
                async (Guid planId, UpdatePlanCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = body with { PlanId = planId };
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("UpdateBillingPlan")
            .WithSummary("Update a billing plan")
            .RequirePermission(BillingPermissions.Manage);
    }
}
