using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.DeleteDocumentDraft;

public static class DeleteDocumentDraftEndpoint
{
    internal static RouteHandlerBuilder MapDeleteDocumentDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/documents/{documentId:guid}",
                async (Guid documentId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new DeleteDocumentDraftCommand(documentId), ct)))
            .WithName("DeleteInvoiceDocumentDraft")
            .WithSummary("Delete a draft document")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.Delete);
    }
}
