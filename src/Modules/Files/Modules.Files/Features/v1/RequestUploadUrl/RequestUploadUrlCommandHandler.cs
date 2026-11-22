using System.Net;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Quota;
using Trislab.Framework.Shared.Quota;
using Trislab.Framework.Storage.Services;
using Trislab.Modules.Files.Contracts;
using Trislab.Modules.Files.Contracts.v1.Commands;
using Trislab.Modules.Files.Contracts.v1.DTOs;
using Trislab.Modules.Files.Data;
using Trislab.Modules.Files.Domain;
using Trislab.Modules.Files.Localization;
using Trislab.Modules.Files.Services;
using Mediator;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Trislab.Modules.Files.Features.v1.RequestUploadUrl;

public sealed class RequestUploadUrlCommandHandler(
    FilesDbContext db,
    IStorageService storage,
    FileAccessPolicyRegistry policies,
    IQuotaService quotas,
    ICurrentUser currentUser,
    IOptions<FilesOptions> options)
    : ICommandHandler<RequestUploadUrlCommand, PresignedUploadResponse>
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async ValueTask<PresignedUploadResponse> Handle(RequestUploadUrlCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var tenantId = currentUser.GetTenant() ?? throw new UnauthorizedException("invalid tenant")
            .Localized("Error.InvalidTenant");
        var userId = currentUser.GetUserId();
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedException("no current user").Localized("Error.NoCurrentUser");
        }

        // Category lookup + extension/size validation.
        if (!options.Value.Categories.TryGetValue(cmd.Category, out var category))
        {
            throw new CustomException($"Unknown category '{cmd.Category}'.", (IEnumerable<string>?)null, HttpStatusCode.BadRequest)
                .Localized<FilesResources>("Files.UnknownCategory", cmd.Category);
        }

        var extension = Path.GetExtension(cmd.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !category.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new CustomException(
                $"Extension '{extension}' not allowed for category '{cmd.Category}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest)
                .Localized<FilesResources>("Files.ExtensionNotAllowed", extension, cmd.Category);
        }

        if (cmd.SizeBytes > category.MaxBytes)
        {
            throw new CustomException(
                $"File exceeds max size of {category.MaxBytes} bytes for category '{cmd.Category}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest)
                .Localized<FilesResources>("Files.FileExceedsMaxSize", category.MaxBytes, cmd.Category);
        }

        // Authorization: policy must exist and allow the attach.
        var policy = policies.Resolve(cmd.OwnerType)
            ?? throw new ForbiddenException($"No file access policy registered for owner type '{cmd.OwnerType}'.")
                .Localized<FilesResources>("Files.NoPolicyForOwnerType", cmd.OwnerType);
        if (!await policy.CanAttachAsync(cmd.OwnerId, userId.ToString(), cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("Not allowed to attach files to this owner.")
                .Localized<FilesResources>("Files.NotAllowedToAttach");
        }

        // Quota pre-check (no debit yet — debit happens on finalize with actual bytes).
        var quotaCheck = await quotas.CheckAsync(tenantId, QuotaResource.StorageBytes, cmd.SizeBytes, cancellationToken).ConfigureAwait(false);
        if (!quotaCheck.Allowed)
        {
            throw new CustomException(
                $"Storage quota exceeded ({quotaCheck.CurrentUsage}/{quotaCheck.Limit} bytes).",
                (IEnumerable<string>?)null,
                (HttpStatusCode)507)
                .Localized<FilesResources>("Files.StorageQuotaExceeded", quotaCheck.CurrentUsage, quotaCheck.Limit);
        }

        // Generate id + storage key + presigned URL.
        var id = Guid.CreateVersion7();
        var storageKey = StorageKeyBuilder.Build(tenantId, cmd.OwnerType, id, cmd.FileName, DateTimeOffset.UtcNow, cmd.Visibility);
        var ttl = TimeSpan.FromMinutes(options.Value.UploadUrlTtlMinutes);
        // The presigned PUT is signed for this Content-Type and the object is served with it, so it comes
        // from the validated extension, never from the client: "photo.png" declared as text/html would
        // otherwise be stored, and later served, as HTML (stored XSS). The browser sends the
        // RequiredHeaders it gets back, so it always matches the signature.
        var contentType = ContentTypes.TryGetContentType(storageKey, out var mapped) ? mapped : "application/octet-stream";
        var presigned = await storage.GenerateUploadUrlAsync(storageKey, contentType, category.MaxBytes, ttl, cancellationToken).ConfigureAwait(false);

        var asset = FileAsset.CreatePending(
            id: id,
            ownerType: cmd.OwnerType,
            ownerId: cmd.OwnerId,
            originalFileName: cmd.FileName,
            sanitizedFileName: StorageKeyBuilder.Sanitize(cmd.FileName),
            contentType: contentType,
            declaredSizeBytes: cmd.SizeBytes,
            storageKey: storageKey,
            visibility: cmd.Visibility,
            createdByUserId: userId.ToString(),
            uploadDeadline: DateTimeOffset.UtcNow.Add(ttl));

        db.FileAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PresignedUploadResponse(asset.Id, presigned.Url, presigned.RequiredHeaders, presigned.ExpiresAt);
    }
}
