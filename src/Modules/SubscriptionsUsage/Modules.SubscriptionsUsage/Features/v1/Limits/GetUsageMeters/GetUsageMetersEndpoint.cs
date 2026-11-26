using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.Authorization;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Limits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Limits.GetUsageMeters;

public static class GetUsageMetersEndpoint
{
    internal static RouteHandlerBuilder MapGetUsageMetersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/meters",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new GetUsageMetersQuery(), ct))
            .WithName("GetUsageMeters")
            .WithSummary("List the usage meters the product declared, for setting a plan's limits")
            .RequirePermission(BillingPermissions.Manage);
    }
}
