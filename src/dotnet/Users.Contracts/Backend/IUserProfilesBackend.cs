namespace ActualChat.Users;

public interface IUserProfilesBackend
{
    [ComputeMethod(KeepAliveTime = 10)]
    Task<UserProfile?> Get(string userId, CancellationToken cancellationToken);
    [ComputeMethod(KeepAliveTime = 10)]
    Task<UserProfile?> GetByName(string name, CancellationToken cancellationToken);

    [CommandHandler]
    public Task UpdateStatus(UpdateStatusCommand command, CancellationToken ct = default);

    public record UpdateStatusCommand(string UserId, UserStatus NewStatus) : ICommand<Unit>, IBackendCommand;

}
