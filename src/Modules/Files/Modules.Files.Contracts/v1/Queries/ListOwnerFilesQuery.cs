using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.Files.Contracts.v1.DTOs;
using Mediator;

namespace Trislab.Modules.Files.Contracts.v1.Queries;

/// <summary>
/// The available files attached to one owner (a record of an owning module), newest first, so a module
/// shows its record's attachments without a link table of its own. The owner type's
/// <see cref="IFileAccessPolicy"/> decides who may list them and which of them each caller may see.
/// </summary>
public sealed record ListOwnerFilesQuery(string OwnerType, Guid OwnerId, int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResponse<FileAssetDto>>;
