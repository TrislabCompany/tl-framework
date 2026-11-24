using Trislab.Framework.Core.Domain;

namespace TestHost;

/// <summary>
/// A tenant-scoped row, the smallest entity that exercises the persistence building blocks. It can point at a
/// file the way a product image does: its image URL follows the file when Files moves the object.
/// </summary>
internal sealed class TestItem : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public Guid? FileAssetId { get; private set; }
    public string? ImageUrl { get; private set; }

    private TestItem() { }

    public static TestItem Create(string name, Guid? fileAssetId = null, string? imageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TestItem { Id = Guid.CreateVersion7(), Name = name, FileAssetId = fileAssetId, ImageUrl = imageUrl };
    }

    public void ReplaceImageUrl(string imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);
        ImageUrl = imageUrl;
    }
}
