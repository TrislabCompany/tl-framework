using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.DeleteItem;

public static class DeleteItemEndpoint
{
    internal static RouteHandlerBuilder MapDeleteItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/items/{itemId:guid}",
                async (Guid itemId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new DeleteItemCommand(itemId), ct)))
            .WithName("DeleteInvoiceItem")
            .WithSummary("Delete an item")
            .RequirePermission(InvoiceDocumentsPermissions.Items.Delete);
    }
}
