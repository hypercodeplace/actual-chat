using Stl.Fusion.Interception;

namespace ActualChat.Chat.UI.Blazor.Services;

public sealed class ChatEntryReader
{
    private readonly IChats _chats;
    private readonly MomentClockSet _clocks;

    public Session Session { get; init; } = Session.Null;
    public ChatId ChatId { get; init; }
    public TimeSpan InvalidationWaitTimeout { get; init; } = TimeSpan.FromMilliseconds(50);

    public ChatEntryReader(IChats chats)
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        var services = ((IComputeService)chats).GetServiceProvider();
        _chats = chats;
        _clocks = services.Clocks();
    }

    public async IAsyncEnumerable<ChatEntry> GetAllAfter(
        long minEntryId,
        bool waitForNewEntries,
        [EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        var idRangeComputed = await Computed
            .Capture(ct => _chats.GetIdRange(Session, ChatId, ct), cancellationToken)
            .ConfigureAwait(false);
        var lastReadEntryId = minEntryId - 1;
        var lastTileEnd = lastReadEntryId;
        while (true) {
            var tile = ChatConstants.IdTiles.GetMinCoveringTile(lastTileEnd + 1);
            lastTileEnd = tile.End;

            var entriesComputed = await Computed
                .Capture(ct => _chats.GetEntries(Session, ChatId, tile, ct), cancellationToken)
                .ConfigureAwait(false);

            var entries = entriesComputed.Value;
            foreach (var entry in entries) {
                if (entry.Id <= lastReadEntryId)
                    continue;

                lastReadEntryId = entry.Id;
                yield return entry; // Note that this "yield" can take arbitrary long time
            }
            var idRange = idRangeComputed.Value;
            var isLastTile = entries.LastOrDefault()?.Id >= idRange.End;
            if (isLastTile) {
                lastTileEnd = tile.Start - 1;

                // Update is ~ free when the computed is consistent
                idRangeComputed = await idRangeComputed.Update(cancellationToken).ConfigureAwait(false);
                var maxEntryId = idRangeComputed.Value.End;
                var lastTile = ChatConstants.IdTiles.GetMinCoveringTile(maxEntryId);
                if (tile.Start < lastTile.Start) {
                    // not the one that includes the very last chat entry
                    lastReadEntryId = tile.End;
                    continue;
                }
                // We're either at the very last tile or maybe even on the next one
                if (!waitForNewEntries)
                    yield break; // And since there are 0 entries...

                // Ok, we're waiting for new entries.
                // 1. This should happen for sure if we already read the last entry:
                if (lastReadEntryId == maxEntryId) {
                    await idRangeComputed.WhenInvalidated(cancellationToken).ConfigureAwait(false);
                    continue;
                }
                // 2. And this should also happen unless _somehow_ the inserted entry landed
                //    on the next tile (i.e. it was either N-times-rollback scenario or something similar).
                //    In this case we want to probably adjust the tile by re-entering this block
                //    after timeout.
                await entriesComputed.WhenInvalidated(cancellationToken)
                    .WithTimeout(_clocks.CpuClock, InvalidationWaitTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task<long> GetNextEntryId(Moment minBeginsAt, CancellationToken cancellationToken)
    {
        // Let's bisect (minId, maxId) range to find the right entry
        var entryId = 0L;
        var (minId, maxId) = await _chats.GetIdRange(Session, ChatId, cancellationToken).ConfigureAwait(false);
        while (minId < maxId) {
            entryId = minId + ((maxId - minId) >> 1);
            var entry = await Get(entryId, maxId, cancellationToken).ConfigureAwait(false);
            if (entry == null)
                maxId = entryId;
            else {
                if (minBeginsAt == entry.BeginsAt) {
                    var prevEntry = await Get(entryId - 1, maxId, cancellationToken).ConfigureAwait(false);
                    if (prevEntry != null && minBeginsAt == prevEntry.BeginsAt)
                        return entryId - 1;

                    return entryId;
                }

                if (minBeginsAt > entry.BeginsAt && !entry.IsStreaming)
                    minId = entryId + 1;
                else
                    maxId = entryId;
            }
        }
        return minId;
    }

    public async Task<ChatEntry?> Get(long entryId, CancellationToken cancellationToken)
    {
        var tile = ChatConstants.IdTiles.GetMinCoveringTile(entryId);
        var entries = await _chats.GetEntries(Session, ChatId, tile, cancellationToken).ConfigureAwait(false);
        return entries.SingleOrDefault(e => e.Id == entryId);
    }

    public async Task<ChatEntry?> Get(long minEntryId, long maxEntryId, CancellationToken cancellationToken)
    {
        var lastTile = ChatConstants.IdTiles.GetMinCoveringTile(maxEntryId);
        while (true) {
            var tile = ChatConstants.IdTiles.GetMinCoveringTile(minEntryId);
            var entries = await _chats.GetEntries(Session, ChatId, tile, cancellationToken).ConfigureAwait(false);
            if (entries.Length == 0) {
                if (tile.Start >= lastTile.Start) // We're at the very last tile
                    return null;

                minEntryId = tile.End + 1;
                continue;
            }
            foreach (var entry in entries)
                if (entry.Id >= minEntryId && entry.Id <= maxEntryId)
                    return entry;

            minEntryId = tile.End + 1;
        }
    }
}
