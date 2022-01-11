using ActualChat.Users;

namespace ActualChat.Chat.UI.Blazor.Components;

public sealed class ChatMessageModel : IEquatable<ChatMessageModel>
{
    private static readonly int MaxBlockLength = 100;
    private static readonly int MaxBlockContentLength = 10_000;
    private static readonly TimeSpan BlockSplitPauseDuration = TimeSpan.FromSeconds(120);

    public string Key { get; }
    public ChatEntry Entry { get; }
    public Author Author { get; }
    public DateOnly? DateLine { get; init; }
    public bool IsBlockStart { get; init; }
    public bool IsBlockEnd { get; init; }

    public ChatMessageModel(ChatEntry entry, Author author)
    {
        Entry = entry;
        Author = author;
        Key = entry.Id.ToString(CultureInfo.InvariantCulture);
    }

    public override string ToString()
        => $"(#{Key} -> {Entry})";

    // Equality

    public bool Equals(ChatMessageModel? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Entry.Equals(other.Entry)
            && Author.Equals(other.Author)
            && Nullable.Equals(DateLine, other.DateLine)
            && IsBlockStart == other.IsBlockStart
            && IsBlockEnd == other.IsBlockEnd;
    }
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj) || obj is ChatMessageModel other && Equals(other);
    public override int GetHashCode()
        => HashCode.Combine(Entry, Author, DateLine, IsBlockStart, IsBlockEnd);
    public static bool operator ==(ChatMessageModel? left, ChatMessageModel? right) => Equals(left, right);
    public static bool operator !=(ChatMessageModel? left, ChatMessageModel? right) => !Equals(left, right);

    // Static helpers

    public static List<KeyValuePair<string, ChatMessageModel>> FromEntries(
        List<ChatEntry> chatEntries,
        Dictionary<Symbol, Author?> authors)
    {
        var result = new List<KeyValuePair<string, ChatMessageModel>>(chatEntries.Count);

        var isBlockStart = true;
        var lastDate = default(DateOnly);
        var blockContentLength = 0;
        var blockLength = 0;

        for (var index = 0; index < chatEntries.Count; index++) {
            if (isBlockStart) {
                blockContentLength = 0;
                blockLength = 0;
            }
            var entry = chatEntries[index];
            var isLastEntry = index == chatEntries.Count - 1;
            var nextEntry = isLastEntry ? null : chatEntries[index + 1];

            var date = DateOnly.FromDateTime(entry.BeginsAt.ToDateTime().ToLocalTime());
            var hasDateLine = date != lastDate;
            var isBlockEnd = blockLength > MaxBlockLength
                || blockContentLength > MaxBlockContentLength
                || ShouldSplit(entry, nextEntry);
            var model = new ChatMessageModel(entry, authors[entry.AuthorId]!) {
                DateLine = hasDateLine ? date : null,
                IsBlockStart = isBlockStart,
                IsBlockEnd = isBlockEnd,
            };
            result.Add(new(model.Key, model));

            isBlockStart = isBlockEnd;
            blockLength += 1;
            blockContentLength += entry.Content.Length;
            lastDate = date;
        }

        return result;

        bool ShouldSplit(ChatEntry entry, ChatEntry? nextEntry)
        {
            if (nextEntry == null)
                return false;
            if (entry.AuthorId != nextEntry.AuthorId)
                return true;
            var prevEndsAt = entry.EndsAt ?? entry.BeginsAt;
            return nextEntry.BeginsAt - prevEndsAt >= BlockSplitPauseDuration;
        }
    }

}
