using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.GetItems;

public static class GetItemsEndpoint
{
    internal static RouteHandlerBuilder MapGetItemsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/items",
                (string? search, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetItemsQuery(search, pageNumber ?? 1, pageSize ?? 20), ct))
            .WithName("GetInvoiceItems")
            .WithSummary("List items")
            .RequirePermission(InvoiceDocumentsPermissions.Items.View);
    }
}
