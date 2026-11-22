using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Storage.Services;
using Trislab.Modules.Files.Contracts;
using Trislab.Modules.Files.Contracts.v1.Commands;
using Trislab.Modules.Files.Contracts.v1.DTOs;
using Trislab.Modules.Files.Data;
using Trislab.Modules.Files.Domain;
using Trislab.Modules.Files.Features.v1.Internal;
using Trislab.Modules.Files.Localization;
using Trislab.Modules.Files.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Files.Features.v1.ChangeVisibility;

public sealed class ChangeFileVisibilityCommandHandler(
    FilesDbContext db,
    FileAccessPolicyRegistry policies,
    ICurrentUser currentUser,
    IStorageService storage,
    FileStorageRelocator relocator)
    : ICommandHandler<ChangeFileVisibilityCommand, FileAssetDto>
{
    public async ValueTask<FileAssetDto> Handle(ChangeFileVisibilityCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (cmd.Visibility is not (Visibility.Public or Visibility.Private))
        {
            throw new CustomException(
                $"Unknown visibility value '{cmd.Visibility}'.",
                errors: null,
                System.Net.HttpStatusCode.BadRequest)
                .Localized<FilesResources>("Files.UnknownVisibility", cmd.Visibility);
        }

        var f = await db.FileAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.FileAssetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");

        var userId = currentUser.GetUserId().ToString();
        var policy = policies.Resolve(f.OwnerType)
            ?? throw new ForbiddenException("no policy").Localized<FilesResources>("Files.NoAccessPolicy");
        var ctx = new FileAccessContext(f.Id, f.OwnerType, f.OwnerId, f.CreatedByUserId, (int)f.Visibility);
        if (!await policy.CanChangeVisibilityAsync(ctx, userId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("not allowed to change this file's visibility")
                .Localized<FilesResources>("Files.NotAllowedToChangeVisibility");
        }

        // Visibility lives in the storage key (public/ vs private/), so a flip moves the object: bucket
        // policies grant anonymous read on public/* only.
        // The relocator re-reads the row under its lock, so a concurrent flip or the legacy-key migration
        // is applied first and this change lands on top of it.
        var changed = await relocator.ApplyAsync(cmd.FileAssetId, asset =>
        {
            if (asset.IsDeleted)
            {
                throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");
            }

            asset.ChangeVisibility(cmd.Visibility);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        f = changed ?? throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");

        var publicUrl = f.Visibility == Visibility.Public
            ? storage.BuildPublicUrl(f.StorageKey)
            : null;
        return FileAssetMapper.ToDto(f, publicUrl);
    }
}
