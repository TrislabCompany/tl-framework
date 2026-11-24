using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Modules.Files.Contracts.Events;

namespace TestHost;

/// <summary>
/// Keeps a test item's image URL pointing at its file when Files moves the object to a new storage key
/// (a visibility change, or the move of legacy public files under <c>public/</c>).
/// </summary>
internal sealed class TestItemFileStorageKeyChangedHandler(TestDbContext db)
    : IIntegrationEventHandler<FileStorageKeyChangedIntegrationEvent>
{
    public async Task HandleAsync(FileStorageKeyChangedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (!string.Equals(@event.OwnerType, TestItemFileAccessPolicy.OwnerTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var items = await db.Items
            .Where(i => i.FileAssetId == @event.FileAssetId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var changed = false;
        foreach (var item in items)
        {
            // Idempotent: once rewritten, the URL no longer ends with the old key and ResolveUrl returns null.
            if (item.ImageUrl is not null && @event.ResolveUrl(item.ImageUrl) is { } rewritten)
            {
                item.ReplaceImageUrl(rewritten);
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
