using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Persistence;
using Trislab.Framework.Storage.Services;
using Trislab.Modules.Files.Contracts;
using Trislab.Modules.Files.Contracts.v1.DTOs;
using Trislab.Modules.Files.Contracts.v1.Queries;
using Trislab.Modules.Files.Data;
using Trislab.Modules.Files.Domain;
using Trislab.Modules.Files.Features.v1.Internal;
using Trislab.Modules.Files.Localization;
using Trislab.Modules.Files.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Files.Features.v1.ListOwnerFiles;

/// <summary>
/// Lists an owner's available files through its owner type's policy: <see cref="IFileAccessPolicy.CanListAsync"/>
/// gates the list, and <see cref="IFileAccessPolicy.CanReadAsync"/> filters it per file before paging, so a page
/// is always full and the total counts only what the caller may see. The policy runs once per file of the owner,
/// which is fine for a record's attachments, not for an owner with thousands of files.
/// </summary>
public sealed class ListOwnerFilesQueryHandler(
    FilesDbContext db,
    FileAccessPolicyRegistry policies,
    ICurrentUser currentUser,
    IStorageService storage)
    : IQueryHandler<ListOwnerFilesQuery, PagedResponse<FileAssetDto>>
{
    public async ValueTask<PagedResponse<FileAssetDto>> Handle(ListOwnerFilesQuery q, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(q);

        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedException("no current user").Localized("Error.NoCurrentUser");
        }

        // Closed by default, as at upload: an owner type without a policy is forbidden.
        var policy = policies.Resolve(q.OwnerType)
            ?? throw new ForbiddenException($"No file access policy registered for owner type '{q.OwnerType}'.")
                .Localized<FilesResources>("Files.NoPolicyForOwnerType", q.OwnerType);
        var caller = userId.ToString();
        if (!await policy.CanListAsync(q.OwnerId, caller, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("Not allowed to list the files of this owner.")
                .Localized<FilesResources>("Files.NotAllowedToList");
        }

        // Uploads store the policy's spelling; the caller's is matched too for files stored before that.
        var ownerType = policy.OwnerType;
        var candidates = await db.FileAssets.AsNoTracking()
            .Where(f => (f.OwnerType == ownerType || f.OwnerType == q.OwnerType)
                && f.OwnerId == q.OwnerId
                && f.Status == FileAssetStatus.Available)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenByDescending(f => f.Id)
            .Select(f => new FileAccessContext(f.Id, f.OwnerType, f.OwnerId, f.CreatedByUserId, (int)f.Visibility))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var readable = new List<Guid>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (await policy.CanReadAsync(candidate, caller, cancellationToken).ConfigureAwait(false))
            {
                readable.Add(candidate.FileAssetId);
            }
        }

        var page = Math.Max(1, q.PageNumber);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var pageIds = readable.Skip(PaginationExtensions.GetOffset(page, size)).Take(size).ToList();

        var rows = await db.FileAssets.AsNoTracking()
            .Where(f => pageIds.Contains(f.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byId = rows.ToDictionary(f => f.Id);

        return new PagedResponse<FileAssetDto>
        {
            // Public files carry their durable URL, as in the other lists, so a preview paints at once.
            Items = pageIds
                .Where(byId.ContainsKey)
                .Select(id => byId[id])
                .Select(f => FileAssetMapper.ToDto(
                    f,
                    f.Visibility == Visibility.Public ? storage.BuildPublicUrl(f.StorageKey) : null))
                .ToList(),
            PageNumber = page,
            PageSize = size,
            TotalCount = readable.Count,
            TotalPages = (int)Math.Ceiling(readable.Count / (double)size),
        };
    }
}
