using Trislab.Modules.Files.Contracts;

namespace TestHost;

/// <summary>
/// The file access policy of the owner type named by <see cref="OwnerTypeName"/>: any signed-in user may attach
/// and read, only the uploader may delete, and nobody may list the files of <see cref="UnlistableOwnerId"/>.
/// It stands in for a product's own policy.
/// </summary>
internal sealed class TestItemFileAccessPolicy : IFileAccessPolicy
{
    public const string OwnerTypeName = "TestItem";

    /// <summary>The owner whose file list is refused, for the tests of a denied list.</summary>
    public static readonly Guid UnlistableOwnerId = new("0b5c1a7e-6f53-4c55-9a51-2f3d8d1c0001");

    public string OwnerType => OwnerTypeName;

    public Task<bool> CanAttachAsync(Guid? ownerId, string currentUserId, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(currentUserId));

    public Task<bool> CanReadAsync(FileAccessContext context, string currentUserId, CancellationToken cancellationToken)
        => Task.FromResult(true);

    public Task<bool> CanDeleteAsync(FileAccessContext context, string currentUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(
            !string.IsNullOrEmpty(currentUserId)
            && string.Equals(currentUserId, context.CreatedByUserId, StringComparison.Ordinal));
    }

    public Task<bool> CanListAsync(Guid? ownerId, string currentUserId, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(currentUserId) && ownerId != UnlistableOwnerId);
}
