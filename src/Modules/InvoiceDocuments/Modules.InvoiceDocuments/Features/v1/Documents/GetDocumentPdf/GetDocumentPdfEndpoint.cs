using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocumentPdf;

public static class GetDocumentPdfEndpoint
{
    internal static RouteHandlerBuilder MapGetDocumentPdfEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/documents/{documentId:guid}/pdf",
                async (Guid documentId, IMediator mediator, CancellationToken ct) =>
                {
                    var file = await mediator.Send(new GetDocumentPdfQuery(documentId), ct).ConfigureAwait(false);
                    return Results.File(file.Content, file.ContentType, file.FileName);
                })
            .WithName("GetInvoiceDocumentPdf")
            .WithSummary("Download an issued document as a PDF")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.View)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status404NotFound);
    }
}
