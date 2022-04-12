using ActualChat.Users.Db;
using Stl.Fusion.EntityFramework;

namespace ActualChat.Users;

public class UserProfiles : DbServiceBase<UsersDbContext>, IUserProfiles
{
    private readonly IAuth _auth;
    private readonly IUserProfilesBackend _backend;
    private readonly ICommander _commander;

    public UserProfiles(IServiceProvider services) : base(services)
    {
        _auth = services.GetRequiredService<IAuth>();
        _backend = services.GetRequiredService<IUserProfilesBackend>();
        _commander = services.GetRequiredService<ICommander>();
    }

    // [ComputeMethod]
    public virtual async Task<UserProfile?> Get(Session session, CancellationToken cancellationToken)
    {
        var user = await _auth.GetUser(session, cancellationToken).ConfigureAwait(false);
        if (!user.IsAuthenticated)
            return null;
        return await _backend.Get(user.Id, cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task UpdateStatus(IUserProfiles.UpdateStatusCommand command, CancellationToken ct = default)
    {
        if (Computed.IsInvalidating())
            return; // It just spawns other commands, so nothing to do here

        var userProfile = await Get(command.Session, ct).ConfigureAwait(false) ?? throw new Exception("User profile is not available");
        if (!userProfile.IsAdmin)
            throw new UnauthorizedAccessException($"User id='{userProfile.User.Id}' is not allowed to update user status");

        await _commander.Call(new IUserProfilesBackend.UpdateStatusCommand(command.UserId, command.NewStatus), ct)
            .ConfigureAwait(false);
    }
}
