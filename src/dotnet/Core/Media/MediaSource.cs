using ActualChat.Channels;

namespace ActualChat.Media;

public interface IMediaSource
{
    MediaFormat Format { get; }
    IAsyncEnumerable<MediaFrame> Frames { get; }
    Task<TimeSpan> DurationTask { get; }
}

public abstract class MediaSource<TFormat, TFrame> : IMediaSource, IAsyncEnumerable<TFrame>
    where TFormat : MediaFormat
    where TFrame : MediaFrame
{
    private readonly AsyncMemoizer<TFrame> _frameMemoizer;
    MediaFormat IMediaSource.Format => Format;
    public TFormat Format { get; }
    public Task<TimeSpan> DurationTask { get; }

    IAsyncEnumerable<MediaFrame> IMediaSource.Frames => Frames;
    public IAsyncEnumerable<TFrame> Frames => GetFrames();

    protected MediaSource(TFormat format, Task<TimeSpan> durationTask, AsyncMemoizer<TFrame> frameMemoizer)
    {
        _frameMemoizer = frameMemoizer;
        Format = format;
        DurationTask = durationTask;
    }

    public IAsyncEnumerator<TFrame> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => Frames.GetAsyncEnumerator(cancellationToken);

    private async IAsyncEnumerable<TFrame> GetFrames()
    {
        var channel = Channel.CreateUnbounded<TFrame>(
            new UnboundedChannelOptions {
                SingleWriter = true
            });

        await _frameMemoizer.AddReplayTarget(channel).ConfigureAwait(false);
        while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
        while (channel.Reader.TryRead(out var frame))
            yield return frame;
    }
}
