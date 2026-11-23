using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocuments;

public static class GetDocumentsEndpoint
{
    internal static RouteHandlerBuilder MapGetDocumentsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/documents",
                (DocumentType? type, DocumentStatus? status, Guid? customerId, bool? openOnly, bool? overdueOnly,
                 DateOnly? from, DateOnly? to, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetDocumentsQuery(
                        type, status, customerId, openOnly ?? false, overdueOnly ?? false, from, to,
                        pageNumber ?? 1, pageSize ?? 20), ct))
            .WithName("GetInvoiceDocuments")
            .WithSummary("List documents; filter by type, status, customer, period, open or overdue")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.View);
    }
}
