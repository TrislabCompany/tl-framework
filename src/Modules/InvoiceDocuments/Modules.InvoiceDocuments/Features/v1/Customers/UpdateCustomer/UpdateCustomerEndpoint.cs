using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.UpdateCustomer;

public static class UpdateCustomerEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCustomerEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/customers/{customerId:guid}",
                async (Guid customerId, UpdateCustomerCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(body with { CustomerId = customerId }, ct));
                })
            .WithName("UpdateInvoiceCustomer")
            .WithSummary("Update a customer")
            .RequirePermission(InvoiceDocumentsPermissions.Customers.Update);
    }
}
