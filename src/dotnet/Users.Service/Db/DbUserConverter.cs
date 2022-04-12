using Stl.Fusion.EntityFramework.Authentication;

namespace ActualChat.Users.Db;

public class DbUserConverter : DbUserConverter<UsersDbContext, DbUser, string>
{
    public DbUserConverter(IServiceProvider services) : base(services)
    { }

    public override void UpdateEntity(User source, DbUser target)
    {
        base.UpdateEntity(source, target);

        // TODO: map unconditionally when user always comes filled in
        if (!source.GetStatusClaim().IsNullOrEmpty() || target.Status == UserStatus.None)
            target.Status = source.GetStatus();
        target.Claims = target.Claims.Remove(UserConstants.Claims.Status);
    }

    public override User UpdateModel(DbUser source, User target)
        => base.UpdateModel(source, target).WithStatusClaim(source.Status);
}
