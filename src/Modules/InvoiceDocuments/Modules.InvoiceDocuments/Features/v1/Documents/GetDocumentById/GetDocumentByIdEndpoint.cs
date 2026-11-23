using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocumentById;

public static class GetDocumentByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetDocumentByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/documents/{documentId:guid}",
                async (Guid documentId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetDocumentByIdQuery(documentId), ct)))
            .WithName("GetInvoiceDocumentById")
            .WithSummary("Get a document with its lines")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.View);
    }
}
