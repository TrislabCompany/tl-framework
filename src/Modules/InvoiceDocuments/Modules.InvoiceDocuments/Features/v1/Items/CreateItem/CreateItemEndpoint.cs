using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Web.Idempotency;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.CreateItem;

public static class CreateItemEndpoint
{
    internal static RouteHandlerBuilder MapCreateItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/items",
                async (CreateItemCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateInvoiceItem")
            .WithSummary("Create an item")
            .RequirePermission(InvoiceDocumentsPermissions.Items.Create)
            .WithIdempotency();
    }
}
