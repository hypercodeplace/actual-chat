namespace ActualChat.Users;

public static class UserExt
{
    public static UserStatus GetStatus(this User user)
    {
        var claimValue = user.Claims.GetValueOrDefault(UserConstants.Claims.Status);

        return Enum.TryParse(claimValue, out UserStatus status)
            ? status
            : throw new ArgumentOutOfRangeException($"Unexpected status claim value '{claimValue}'");
    }

    public static string? GetStatusClaim(this User user) => user.Claims.GetValueOrDefault(UserConstants.Claims.Status);

    public static User WithStatusClaim(this User user, UserStatus status)
        => user.WithClaim(UserConstants.Claims.Status, status.ToString());
}
