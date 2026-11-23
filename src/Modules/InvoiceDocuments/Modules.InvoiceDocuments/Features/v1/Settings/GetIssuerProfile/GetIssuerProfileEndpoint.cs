using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Settings.GetIssuerProfile;

public static class GetIssuerProfileEndpoint
{
    internal static RouteHandlerBuilder MapGetIssuerProfileEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/settings/issuer",
                async (IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetIssuerProfileQuery(), ct)))
            .WithName("GetInvoiceIssuerProfile")
            .WithSummary("Get the tenant's business details")
            .RequirePermission(InvoiceDocumentsPermissions.Settings.View);
    }
}
