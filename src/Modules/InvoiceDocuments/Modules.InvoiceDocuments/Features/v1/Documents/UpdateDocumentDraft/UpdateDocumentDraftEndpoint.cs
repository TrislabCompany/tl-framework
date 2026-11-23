using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.UpdateDocumentDraft;

public static class UpdateDocumentDraftEndpoint
{
    internal static RouteHandlerBuilder MapUpdateDocumentDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/documents/{documentId:guid}",
                async (Guid documentId, UpdateDocumentDraftCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(body with { DocumentId = documentId }, ct));
                })
            .WithName("UpdateInvoiceDocumentDraft")
            .WithSummary("Change a draft document")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.Update);
    }
}
