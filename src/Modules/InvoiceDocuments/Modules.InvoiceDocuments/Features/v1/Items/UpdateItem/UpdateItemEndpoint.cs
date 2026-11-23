using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.UpdateItem;

public static class UpdateItemEndpoint
{
    internal static RouteHandlerBuilder MapUpdateItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/items/{itemId:guid}",
                async (Guid itemId, UpdateItemCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(body with { ItemId = itemId }, ct));
                })
            .WithName("UpdateInvoiceItem")
            .WithSummary("Update an item")
            .RequirePermission(InvoiceDocumentsPermissions.Items.Update);
    }
}
