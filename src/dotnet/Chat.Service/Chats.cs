using System.Security;
using ActualChat.Chat.Db;
using Stl.Fusion.EntityFramework;
using Stl.Redis;

namespace ActualChat.Chat;

public partial class Chats : DbServiceBase<ChatDbContext>, IChats, IChatsBackend
{
    private static readonly TileStack<long> IdTileStack = Constants.Chat.IdTileStack;

    private readonly IAuth _auth;
    private readonly IAuthBackend _authBackend;
    private readonly IChatAuthors _chatAuthors;
    private readonly IChatAuthorsBackend _chatAuthorsBackend;
    private readonly IDbEntityResolver<string, DbChat> _dbChatResolver;
    private readonly RedisSequenceSet<ChatEntry> _idSequences;
    private readonly ICommander _commander;

    public Chats(
        IAuth auth,
        IAuthBackend authBackend,
        IChatAuthors chatAuthors,
        IChatAuthorsBackend chatAuthorsBackend,
        IDbEntityResolver<string, DbChat> dbChatResolver,
        RedisSequenceSet<ChatEntry> idSequences,
        ICommander commander,
        IServiceProvider services) : base(services)
    {
        _auth = auth;
        _authBackend = authBackend;
        _chatAuthors = chatAuthors;
        _chatAuthorsBackend = chatAuthorsBackend;
        _dbChatResolver = dbChatResolver;
        _idSequences = idSequences;
        _commander = commander;
    }

    // [ComputeMethod]
    public virtual async Task<Chat?> Get(Session session, string chatId, CancellationToken cancellationToken)
    {
        var canRead = await CheckHasPermissions(session, chatId, ChatPermissions.Read, cancellationToken).ConfigureAwait(false);
        if (!canRead)
            return null;
        return await Get(chatId, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<ChatTile> GetTile(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long> idTileRange,
        CancellationToken cancellationToken)
    {
        await AssertHasPermissions(session, chatId, ChatPermissions.Read, cancellationToken).ConfigureAwait(false);
        return await GetTile(chatId, entryType, idTileRange, false, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<long> GetEntryCount(
        Session session,
        string chatId,
        ChatEntryType entryType,
        Range<long>? idTileRange,
        CancellationToken cancellationToken)
    {
        await AssertHasPermissions(session, chatId, ChatPermissions.Read, cancellationToken).ConfigureAwait(false);
        return await GetEntryCount(chatId, entryType, idTileRange, false, cancellationToken).ConfigureAwait(false);
    }

    // Note that it returns (firstId, lastId + 1) range!
    // [ComputeMethod]
    public virtual async Task<Range<long>> GetIdRange(
        Session session,
        string chatId,
        ChatEntryType entryType,
        CancellationToken cancellationToken)
    {
        await AssertHasPermissions(session, chatId, ChatPermissions.Read, cancellationToken).ConfigureAwait(false);
        return await GetIdRange(chatId, entryType, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<ChatPermissions> GetPermissions(
        Session session,
        string chatId,
        CancellationToken cancellationToken)
    {
        var chatPrinciaplId = await _chatAuthors.GetSessionChatPrincipalId(session, chatId, cancellationToken).ConfigureAwait(false);
        return await GetPermissions(chatId, chatPrinciaplId, cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task<Chat> CreateChat(IChats.CreateChatCommand command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return default!; // It just spawns other commands, so nothing to do here

        var (session, title) = command;
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        user.MustBeAuthenticated();

        var chat = new Chat() {
            Title = title,
            OwnerIds = ImmutableArray.Create(user.Id),
        };
        var createChatCommand = new IChatsBackend.CreateChatCommand(chat);
        return await _commander.Call(createChatCommand, true, cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task<ChatEntry> CreateEntry(
        IChats.CreateEntryCommand command,
        CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return default!; // It just spawns other commands, so nothing to do here

        var (session, chatId, text) = command;
        await AssertHasPermissions(session, chatId, ChatPermissions.Write, cancellationToken).ConfigureAwait(false);
        var author = await _chatAuthorsBackend.GetOrCreate(session, chatId, cancellationToken).ConfigureAwait(false);

        var chatEntry = new ChatEntry() {
            ChatId = chatId,
            AuthorId = author.Id,
            Content = text,
            Type = ChatEntryType.Text,
        };
        var upsertCommand = new IChatsBackend.UpsertEntryCommand(chatEntry);
        return await _commander.Call(upsertCommand, true, cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task RemoveTextEntry(IChats.RemoveTextEntryCommand command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return; // It just spawns other commands, so nothing to do here

        var (session, chatId, entryId) = command;
        await AssertHasPermissions(session, chatId, ChatPermissions.Write, cancellationToken).ConfigureAwait(false);
        var author = await _chatAuthorsBackend.GetOrCreate(session, chatId, cancellationToken).ConfigureAwait(false);

        var idTile = IdTileStack.FirstLayer.GetTile(entryId);
        var tile = await GetTile(session, chatId, ChatEntryType.Text, idTile.Range, cancellationToken).ConfigureAwait(false);
        var chatEntry = tile.Entries.Single(e => e.Id == entryId);
        if (chatEntry.AuthorId != author.Id)
            throw new SecurityException("You can delete only your own messages.");

        chatEntry = chatEntry with { IsRemoved = true };
        var upsertCommand = new IChatsBackend.UpsertEntryCommand(chatEntry);
        await _commander.Call(upsertCommand, true, cancellationToken).ConfigureAwait(false);
    }
}
