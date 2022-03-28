using Microsoft.Extensions.Hosting;

namespace ActualChat.UI.Blazor.Services;

public record BlazorHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted { get; } = new CancellationToken(true);
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() { }
}
