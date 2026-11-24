using Trislab.Modules.Files.Contracts;

namespace TestHost;

/// <summary>
/// The file access policy of the owner type named by <see cref="OwnerTypeName"/>: any signed-in user may attach
/// and list, a public file is read by anyone and a private one only by its uploader, who alone may delete. It
/// stands in for a product's policy whose list is filtered per file.
/// </summary>
internal sealed class TestDocumentFileAccessPolicy : IFileAccessPolicy
{
    public const string OwnerTypeName = "TestDocument";

    public string OwnerType => OwnerTypeName;

    public Task<bool> CanAttachAsync(Guid? ownerId, string currentUserId, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(currentUserId));

    public Task<bool> CanReadAsync(FileAccessContext context, string currentUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(context.Visibility == 0 || IsUploader(context, currentUserId));
    }

    public Task<bool> CanDeleteAsync(FileAccessContext context, string currentUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(IsUploader(context, currentUserId));
    }

    private static bool IsUploader(FileAccessContext context, string currentUserId)
        => !string.IsNullOrEmpty(currentUserId)
            && string.Equals(currentUserId, context.CreatedByUserId, StringComparison.Ordinal);
}
