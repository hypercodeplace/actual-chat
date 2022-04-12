using Microsoft.AspNetCore.Mvc;
using Stl.Fusion.Server;

namespace ActualChat.Users.Controllers;

[Route("api/[controller]/[action]")]
[ApiController, JsonifyErrors]
public class UserProfilesController : ControllerBase, IUserProfiles
{
    private readonly IUserProfiles _service;

    public UserProfilesController(IUserProfiles service)
        => _service = service;

    [HttpGet, Publish]
    public Task<UserProfile?> Get(Session session, CancellationToken cancellationToken)
        => _service.Get(session, cancellationToken);

    [HttpPost]
    public Task UpdateStatus([FromBody] IUserProfiles.UpdateStatusCommand command, CancellationToken ct = default)
        => _service.UpdateStatus(command, ct);
}
