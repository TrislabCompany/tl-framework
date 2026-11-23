using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.DeleteCustomer;

public static class DeleteCustomerEndpoint
{
    internal static RouteHandlerBuilder MapDeleteCustomerEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/customers/{customerId:guid}",
                async (Guid customerId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new DeleteCustomerCommand(customerId), ct)))
            .WithName("DeleteInvoiceCustomer")
            .WithSummary("Delete a customer")
            .RequirePermission(InvoiceDocumentsPermissions.Customers.Delete);
    }
}
