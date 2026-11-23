using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Customers.GetCustomers;

public static class GetCustomersEndpoint
{
    internal static RouteHandlerBuilder MapGetCustomersEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/customers",
                (string? search, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetCustomersQuery(search, pageNumber ?? 1, pageSize ?? 20), ct))
            .WithName("GetInvoiceCustomers")
            .WithSummary("List customers")
            .RequirePermission(InvoiceDocumentsPermissions.Customers.View);
    }
}
