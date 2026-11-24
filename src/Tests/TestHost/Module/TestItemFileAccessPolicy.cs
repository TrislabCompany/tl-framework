using Trislab.Modules.Files.Contracts;

namespace TestHost;

/// <summary>
/// The file access policy of the owner type named by <see cref="OwnerTypeName"/>: any signed-in user may attach
/// and read, only the uploader may delete. It stands in for a product's own policy.
/// </summary>
internal sealed class TestItemFileAccessPolicy : IFileAccessPolicy
{
    public const string OwnerTypeName = "TestItem";

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
}
