using Trislab.Modules.SubscriptionsUsage.Contracts.Authorization;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Web.Idempotency;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Plans;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Plans.CreatePlan;

public static class CreatePlanEndpoint
{
    internal static RouteHandlerBuilder MapCreatePlanEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/plans",
                async (CreatePlanCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateBillingPlan")
            .WithSummary("Create a new billing plan")
            .RequirePermission(BillingPermissions.Manage)
            .WithIdempotency();
    }
}
