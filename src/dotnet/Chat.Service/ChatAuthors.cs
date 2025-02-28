using ActualChat.Chat.Db;
using ActualChat.Users;
using Stl.Fusion.EntityFramework;
using Stl.Redis;

namespace ActualChat.Chat;

public partial class ChatAuthors : DbServiceBase<ChatDbContext>, IChatAuthors, IChatAuthorsBackend
{
    private const string AuthorIdSuffix = "::authorId";

    private readonly ICommander _commander;
    private readonly IAuth _auth;
    private readonly IAuthBackend _authBackend;
    private readonly IUserAuthorsBackend _userAuthorsBackend;
    private readonly IUserAvatarsBackend _userAvatarsBackend;
    private readonly RedisSequenceSet<ChatAuthor> _idSequences;
    private readonly IRandomNameGenerator _randomNameGenerator;
    private readonly IDbEntityResolver<string, DbChatAuthor> _dbChatAuthorResolver;
    private readonly IChatUserSettingsBackend _chatUserSettingsBackend;
    private readonly IUserContactsBackend _userContactsBackend;

    public ChatAuthors(IServiceProvider services) : base(services)
    {
        _commander = services.Commander();
        _auth = Services.GetRequiredService<IAuth>();
        _authBackend = Services.GetRequiredService<IAuthBackend>();
        _userAuthorsBackend = services.GetRequiredService<IUserAuthorsBackend>();
        _idSequences = services.GetRequiredService<RedisSequenceSet<ChatAuthor>>();
        _randomNameGenerator = services.GetRequiredService<IRandomNameGenerator>();
        _dbChatAuthorResolver = services.GetRequiredService<IDbEntityResolver<string, DbChatAuthor>>();
        _userAvatarsBackend = services.GetRequiredService<IUserAvatarsBackend>();
        _chatUserSettingsBackend = services.GetRequiredService<IChatUserSettingsBackend>();
        _userContactsBackend = services.GetRequiredService<IUserContactsBackend>();
    }

    // [ComputeMethod]
    public virtual async Task<ChatAuthor?> GetChatAuthor(
        Session session, string chatId,
        CancellationToken cancellationToken)
    {
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (user.IsAuthenticated)
            return await GetByUserId(chatId, user.Id, false, cancellationToken).ConfigureAwait(false);

        var options = await _auth.GetOptions(session, cancellationToken).ConfigureAwait(false);
        var authorId = options[chatId + AuthorIdSuffix] as string;
        if (authorId == null)
            return null;
        return await Get(chatId, authorId, false, cancellationToken).ConfigureAwait(false);
    }

    // [ComputeMethod]
    public virtual async Task<string> GetChatPrincipalId(
        Session session, string chatId,
        CancellationToken cancellationToken)
    {
        var author = await GetChatAuthor(session, chatId, cancellationToken).ConfigureAwait(false);
        if (author != null)
            return author.Id;
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        return user.IsAuthenticated ? user.Id : "";
    }

    // [ComputeMethod]
    public virtual async Task<Author?> GetAuthor(
        string chatId, string authorId, bool inherit,
        CancellationToken cancellationToken)
    {
        var chatAuthor = await Get(chatId, authorId, inherit, cancellationToken).ConfigureAwait(false);
        return chatAuthor.ToAuthor();
    }

    // [ComputeMethod]
    public virtual async Task<string[]> GetChatIds(Session session, CancellationToken cancellationToken)
    {
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (user.IsAuthenticated)
            return await GetChatIdsByUserId(user.Id, cancellationToken).ConfigureAwait(false);

        var options = await _auth.GetOptions(session, cancellationToken).ConfigureAwait(false);
        var chatIds = options.Items.Keys
            .Select(c => c.Value)
            .Where(c => c.EndsWith(AuthorIdSuffix, StringComparison.Ordinal))
            .Select(c => c.Substring(0, c.Length - AuthorIdSuffix.Length))
            .ToArray();
        return chatIds;
    }

    // [ComputeMethod]
    public virtual async Task<string?> GetChatAuthorAvatarId(Session session, string chatId, CancellationToken cancellationToken)
    {
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (user.IsAuthenticated)
            return null;
        var chatAuthor = await GetChatAuthor(session, chatId, cancellationToken).ConfigureAwait(false);
        if (chatAuthor == null)
            return null;
        var avatar = await _userAvatarsBackend.EnsureChatAuthorAvatarCreated(chatAuthor.Id, "", cancellationToken)
            .ConfigureAwait(false);
        return avatar.Id;
    }

    public virtual async Task<bool> CanAddToContacts(Session session, string chatAuthorId, CancellationToken cancellationToken)
    {
        var (userId1, user2Id) = await GetPeerChatUserIds(session, chatAuthorId, cancellationToken).ConfigureAwait(false);
        if (user2Id.IsNullOrEmpty())
            return false;
        var contact = await _userContactsBackend.GetByTargetId(userId1, user2Id, cancellationToken).ConfigureAwait(false);
        return contact == null;
    }

    public virtual async Task AddToContacts(IChatAuthors.AddToContactsCommand command, CancellationToken cancellationToken)
    {
        if (Computed.IsInvalidating())
            return; // It just spawns other commands, so nothing to do here
        var (session, chatAuthorId) = command;
        var (userId1, user2Id) = await GetPeerChatUserIds(session, chatAuthorId, cancellationToken).ConfigureAwait(false);
        if (user2Id.IsNullOrEmpty())
            return;
        _ = await _userContactsBackend.GetOrCreate(userId1, user2Id, cancellationToken).ConfigureAwait(false);
        _ = await _userContactsBackend.GetOrCreate(user2Id, userId1, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string, string)> GetPeerChatUserIds(Session session, string chatAuthorId, CancellationToken cancellationToken)
    {
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (!user.IsAuthenticated)
            return ("","");
        if (!ChatAuthor.TryGetChatId(chatAuthorId, out var chatId))
            return ("","");
        var chatAuthor = await Get(chatId, chatAuthorId, false, cancellationToken)
            .ConfigureAwait(false);
        if (chatAuthor == null || chatAuthor.UserId.IsEmpty)
            return ("","");
        if (user.Id == chatAuthor.UserId)
            return ("","");
        var user2 = await _authBackend.GetUser(chatAuthor.UserId, cancellationToken).ConfigureAwait(false);
        if (user2 == null)
            return ("","");
        return (user.Id, user2.Id);
    }
}
