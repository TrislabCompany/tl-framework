using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.IssueDocument;

public static class IssueDocumentEndpoint
{
    internal static RouteHandlerBuilder MapIssueDocumentEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/documents/{documentId:guid}/issue",
                async (Guid documentId, DateOnly? issueDate, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new IssueDocumentCommand(documentId, issueDate), ct)))
            .WithName("IssueInvoiceDocument")
            .WithSummary("Issue a draft: give it its number and lock it")
            .RequirePermission(InvoiceDocumentsPermissions.Documents.Issue);
    }
}
