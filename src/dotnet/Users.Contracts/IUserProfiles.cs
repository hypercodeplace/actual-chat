namespace ActualChat.Users;

public interface IUserProfiles
{
    [ComputeMethod(KeepAliveTime = 10)]
    Task<UserProfile?> Get(Session session, CancellationToken cancellationToken);

    [CommandHandler]
    public Task UpdateStatus(UpdateStatusCommand command, CancellationToken ct = default);

    public record UpdateStatusCommand(string UserId, UserStatus NewStatus, Session Session) : ISessionCommand<Unit>;
}
