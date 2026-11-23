using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ExportDocuments;

public static class ExportDocumentsEndpoint
{
    internal static RouteHandlerBuilder MapExportDocumentsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/documents/export",
                async (DateOnly from, DateOnly to, IMediator mediator, CancellationToken ct) =>
                {
                    var file = await mediator.Send(new ExportDocumentsQuery(from, to), ct).ConfigureAwait(false);
                    return Results.File(file.Content, file.ContentType, file.FileName);
                })
            .WithName("ExportInvoiceDocuments")
            .WithSummary("Download the documents issued in a period for the accountant (a zip with a CSV and the PDFs)")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.Export)
            .Produces(StatusCodes.Status200OK, contentType: "application/zip");
    }
}
