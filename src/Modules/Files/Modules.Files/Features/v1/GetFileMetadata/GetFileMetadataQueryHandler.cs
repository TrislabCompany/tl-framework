using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
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

namespace Trislab.Modules.Files.Features.v1.GetFileMetadata;

public sealed class GetFileMetadataQueryHandler(
    FilesDbContext db,
    FileAccessPolicyRegistry policies,
    ICurrentUser currentUser,
    IStorageService storage)
    : IQueryHandler<GetFileMetadataQuery, FileAssetDto>
{
    public async ValueTask<FileAssetDto> Handle(GetFileMetadataQuery q, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(q);

        var f = await db.FileAssets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.FileAssetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");

        var userId = currentUser.GetUserId().ToString();
        // don't leak existence on missing policy
        var policy = policies.Resolve(f.OwnerType)
            ?? throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");

        var ctx = new FileAccessContext(f.Id, f.OwnerType, f.OwnerId, f.CreatedByUserId, (int)f.Visibility);
        if (!await policy.CanReadAsync(ctx, userId, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");
        }

        // Public files get a durable URL safe to persist long-term, while private files mint a
        // short-lived presigned GET on demand via the auth-gated url endpoint.
        var publicUrl = f.Visibility == Visibility.Public
            ? storage.BuildPublicUrl(f.StorageKey)
            : null;

        return FileAssetMapper.ToDto(f, publicUrl);
    }
}
