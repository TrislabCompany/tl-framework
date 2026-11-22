using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Files.Contracts;
using Trislab.Modules.Files.Contracts.v1.Commands;
using Trislab.Modules.Files.Data;
using Trislab.Modules.Files.Localization;
using Trislab.Modules.Files.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Files.Features.v1.DeleteFile;

public sealed class DeleteFileCommandHandler(
    FilesDbContext db,
    FileAccessPolicyRegistry policies,
    ICurrentUser currentUser)
    : ICommandHandler<DeleteFileCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteFileCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var f = await db.FileAssets
            .FirstOrDefaultAsync(x => x.Id == cmd.FileAssetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("file not found").Localized<FilesResources>("Files.FileNotFound");

        var userId = currentUser.GetUserId().ToString();
        var policy = policies.Resolve(f.OwnerType)
            ?? throw new ForbiddenException("no policy").Localized<FilesResources>("Files.NoAccessPolicy");
        var ctx = new FileAccessContext(f.Id, f.OwnerType, f.OwnerId, f.CreatedByUserId, (int)f.Visibility);
        if (!await policy.CanDeleteAsync(ctx, userId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("not allowed to delete this file")
                .Localized<FilesResources>("Files.NotAllowedToDelete");
        }

        // Soft-delete: AuditableEntitySaveChangesInterceptor sets IsDeleted/DeletedOnUtc/DeletedBy on
        // Remove() for ISoftDeletable; byte purge runs later via PurgeDeletedFilesJob post-retention.
        db.FileAssets.Remove(f);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
