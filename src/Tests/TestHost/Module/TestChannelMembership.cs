using System.Collections.Concurrent;
using Trislab.Framework.Web.Realtime;

namespace TestHost;

/// <summary>
/// An in-memory channel membership, the smallest thing that satisfies the realtime hub's two lookups.
/// A product's chat keeps this in its database; the hub only asks the two interfaces.
/// </summary>
internal sealed class TestChannelMembership : IChannelMembershipChecker, IUserChannelLookup
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _channels = new();

    public Guid CreateChannel(string creatorUserId)
    {
        var id = Guid.CreateVersion7();
        _channels[id] = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        Add(id, creatorUserId);
        return id;
    }

    public bool Add(Guid channelId, string userId)
    {
        if (!_channels.TryGetValue(channelId, out var members))
        {
            return false;
        }

        members[userId] = 0;
        return true;
    }

    public ValueTask<bool> IsMemberAsync(Guid channelId, string userId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            !string.IsNullOrEmpty(userId)
            && _channels.TryGetValue(channelId, out var members)
            && members.ContainsKey(userId));

    public ValueTask<IReadOnlyList<Guid>> ListMyChannelIdsAsync(string userId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<Guid>>(
            string.IsNullOrEmpty(userId)
                ? []
                : _channels.Where(c => c.Value.ContainsKey(userId)).Select(c => c.Key).ToList());
}
