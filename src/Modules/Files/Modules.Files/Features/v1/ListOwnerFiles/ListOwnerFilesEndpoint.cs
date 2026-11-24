using Trislab.Framework.Shared.Identity.Authorization;
using Trislab.Modules.Files.Contracts.Authorization;
using Trislab.Modules.Files.Contracts.v1.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Files.Features.v1.ListOwnerFiles;

public static class ListOwnerFilesEndpoint
{
    internal static RouteHandlerBuilder MapListOwnerFilesEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("",
                async (string ownerType, Guid ownerId, int? pageNumber, int? pageSize, IMediator mediator, CancellationToken cancellationToken) =>
                    Results.Ok(await mediator.Send(
                        new ListOwnerFilesQuery(ownerType, ownerId, pageNumber ?? 1, pageSize ?? 20), cancellationToken)))
            .WithName("ListOwnerFiles")
            .WithSummary("List the files attached to one owner, as its owner type's policy allows")
            .RequirePermission(FilesPermissions.Upload);
}
