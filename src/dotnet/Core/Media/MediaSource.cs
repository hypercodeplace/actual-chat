using ActualChat.Channels;

namespace ActualChat.Media;

public interface IMediaSource
{
    MediaFormat Format { get; }
    IAsyncEnumerable<MediaFrame> Frames { get; }
    Task<TimeSpan> Duration { get; }
}

public abstract class MediaSource<TFormat, TFrame> : IMediaSource, IAsyncEnumerable<TFrame>
    where TFormat : MediaFormat
    where TFrame : MediaFrame
{
    private readonly AsyncMemoizer<TFrame> _framesMemoizer;
    MediaFormat IMediaSource.Format => Format;
    public TFormat Format { get; }
    public Task<TimeSpan> Duration { get; }

    IAsyncEnumerable<MediaFrame> IMediaSource.Frames => Frames;
    public IAsyncEnumerable<TFrame> Frames => GetFrames();

    protected MediaSource(TFormat format, Task<TimeSpan> durationTask, AsyncMemoizer<TFrame> framesMemoizer)
    {
        _framesMemoizer = framesMemoizer;
        Format = format;
        Duration = durationTask;
    }

    public IAsyncEnumerator<TFrame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => Frames.GetAsyncEnumerator(cancellationToken);

    private async IAsyncEnumerable<TFrame> GetFrames()
    {
        var channel = Channel.CreateUnbounded<TFrame>(
            new UnboundedChannelOptions {
                SingleWriter = true
            });

        await _framesMemoizer.AddReplayTarget(channel).ConfigureAwait(false);
        while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
        while (channel.Reader.TryRead(out var frame))
            yield return frame;
    }
}
