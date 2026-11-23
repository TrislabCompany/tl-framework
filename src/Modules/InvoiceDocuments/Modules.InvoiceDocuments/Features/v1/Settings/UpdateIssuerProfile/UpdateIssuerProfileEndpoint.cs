using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Settings.UpdateIssuerProfile;

public static class UpdateIssuerProfileEndpoint
{
    internal static RouteHandlerBuilder MapUpdateIssuerProfileEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/settings/issuer",
                async (UpdateIssuerProfileCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("UpdateInvoiceIssuerProfile")
            .WithSummary("Save the tenant's business details")
            .RequirePermission(InvoiceDocumentsPermissions.Settings.Update);
    }
}
