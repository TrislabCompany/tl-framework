using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.MarkDocumentPaid;

public static class MarkDocumentPaidEndpoint
{
    internal static RouteHandlerBuilder MapMarkDocumentPaidEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/documents/{documentId:guid}/mark-paid",
                async (Guid documentId, DateTime? paidOnUtc, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new MarkDocumentPaidCommand(documentId, paidOnUtc), ct)))
            .WithName("MarkInvoiceDocumentPaid")
            .WithSummary("Mark an issued invoice paid, by hand")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.MarkPaid);
    }
}
