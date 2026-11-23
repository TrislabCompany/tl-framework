using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Web.Idempotency;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.CreateCustomer;

public static class CreateCustomerEndpoint
{
    internal static RouteHandlerBuilder MapCreateCustomerEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/customers",
                async (CreateCustomerCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateInvoiceCustomer")
            .WithSummary("Create a customer")
            .RequirePermission(InvoiceDocumentsPermissions.Customers.Create)
            .WithIdempotency();
    }
}
