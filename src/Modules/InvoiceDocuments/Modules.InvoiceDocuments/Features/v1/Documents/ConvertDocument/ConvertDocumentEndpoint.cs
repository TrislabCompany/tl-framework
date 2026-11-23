using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ConvertDocument;

public static class ConvertDocumentEndpoint
{
    internal static RouteHandlerBuilder MapConvertDocumentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/documents/{documentId:guid}/convert",
                async (Guid documentId, DocumentType targetType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ConvertDocumentCommand(documentId, targetType), ct)))
            .WithName("ConvertInvoiceDocument")
            .WithSummary("Convert an issued quote or pro forma into a draft of another type")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.Create);
    }
}
