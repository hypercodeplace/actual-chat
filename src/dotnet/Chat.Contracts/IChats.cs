namespace ActualChat.Chat;

public interface IChats
{
    [ComputeMethod(KeepAliveTime = 1)]
    Task<Chat?> Get(Session session, string chatId, CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<Chat[]> GetChats(Session session, CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<long> GetEntryCount(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long>? idTileRange,
        CancellationToken cancellationToken);

    // Note that it returns (firstId, lastId + 1) range!
    [ComputeMethod(KeepAliveTime = 1)]
    Task<Range<long>> GetIdRange(
        Session session,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken);

    // Client-side method always skips entries with IsRemoved flag
    [ComputeMethod(KeepAliveTime = 1)]
    Task<ChatTile> GetTile(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long> idTileRange,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<ChatPermissions> GetPermissions(
        Session session,
        string chatId,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<bool> CheckCanJoin(
        Session session,
        string chatId,
        CancellationToken cancellationToken);
    [ComputeMethod(KeepAliveTime = 1)]
    Task<ImmutableArray<TextEntryAttachment>> GetTextEntryAttachments(
        Session session, string chatId, long entryId,
        CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<bool> CanSendUserPeerChatMessage(Session session, string chatAuthorId, CancellationToken cancellationToken);

    [ComputeMethod(KeepAliveTime = 1)]
    Task<string?> GetUserPeerChatId(Session session, string chatAuthorId, CancellationToken cancellationToken);

    // Commands

    [CommandHandler]
    Task<Chat> CreateChat(CreateChatCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task<Unit> UpdateChat(UpdateChatCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task<Unit> JoinChat(JoinChatCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task<ChatEntry> CreateTextEntry(CreateTextEntryCommand command, CancellationToken cancellationToken);
    [CommandHandler]
    Task RemoveTextEntry(RemoveTextEntryCommand command, CancellationToken cancellationToken);

    public record CreateChatCommand(Session Session, string Title) : ISessionCommand<Chat>
    {
        public bool IsPublic { get; init; }
    }

    public record UpdateChatCommand(Session Session, Chat Chat) : ISessionCommand<Unit>;
    public record JoinChatCommand(Session Session, string ChatId) : ISessionCommand<Unit>;
    public record CreateTextEntryCommand(Session Session, string ChatId, string Text) : ISessionCommand<ChatEntry>
    {
        public ImmutableArray<TextEntryAttachmentUpload> Attachments { get; set; } = ImmutableArray<TextEntryAttachmentUpload>.Empty;
    }
    public record RemoveTextEntryCommand(Session Session, string ChatId, long EntryId) : ISessionCommand<Unit>;
}
