using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Framework.Web.Idempotency;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.CreateDocumentDraft;

public static class CreateDocumentDraftEndpoint
{
    internal static RouteHandlerBuilder MapCreateDocumentDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/documents",
                async (CreateDocumentDraftCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateInvoiceDocumentDraft")
            .WithSummary("Create a draft quote, pro forma, invoice, credit note or advance invoice")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.Create)
            .WithIdempotency();
    }
}
