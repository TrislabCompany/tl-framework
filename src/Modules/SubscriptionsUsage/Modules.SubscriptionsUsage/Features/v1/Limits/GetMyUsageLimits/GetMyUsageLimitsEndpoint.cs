using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.GetMyUsageLimits;

public static class GetMyUsageLimitsEndpoint
{
    internal static RouteHandlerBuilder MapGetMyUsageLimitsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/limits/me",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new GetMyUsageLimitsQuery(), ct))
            .WithName("GetMyUsageLimits")
            .WithSummary("The current tenant's usage of each meter against its plan's limits")
            .RequirePermission(BillingPermissions.View);
    }
}
