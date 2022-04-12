using ActualChat.Host;
using ActualChat.Testing.Host;
using Microsoft.Extensions.Configuration;

namespace ActualChat.Users.IntegrationTests;

public class UserStatusTest : AppHostTestBase
{
    private static readonly UserStatus NewUserDefaultStatus = UserStatus.Active;

    private WebClientTester _tester = null!;

    private IUserProfiles _userProfiles = null!;

    private AppHost _appHost = null!;

    public UserStatusTest(ITestOutputHelper @out) : base(@out)
    { }

    public override async Task InitializeAsync()
    {
        _appHost = await TestHostFactory.NewAppHost(host => host.AppConfigurationBuilder = builder
            => builder.AddInMemoryCollection(new Dictionary<string, string> {
                ["UsersSettings:NewUserDefaultStatus"] = NewUserDefaultStatus.ToString(),
            }));
        _tester = _appHost.NewWebClientTester();
        _userProfiles = _appHost.Services.GetRequiredService<IUserProfiles>();
    }

    public override async Task DisposeAsync()
    {
        await _tester.DisposeAsync().AsTask();
        _appHost.Dispose();
    }

    [Fact]
    public async Task ShouldUpdateStatus()
    {
        // arrange
        var user = new User("", "Bob");

        // act
        user = await _tester.SignIn(user);
        var status = user.GetStatus();

        // assert
        status.Should().Be(NewUserDefaultStatus);

        // act
        foreach (var newStatus in new[] {
                     UserStatus.Inactive, UserStatus.Suspended, UserStatus.Active, UserStatus.Inactive,
                     UserStatus.Suspended, UserStatus.Active,
                 })
        {
            Out.WriteLine($"Changing user status {user.GetStatus()} => {newStatus}");
            await _userProfiles.UpdateStatus(new IUserProfiles.UpdateStatusCommand(user.Id, newStatus, _tester.Session));

            // assert
            user = await _tester.Auth.GetUser(_tester.Session);
            user.GetStatus().Should().Be(newStatus);
        }
    }
}
