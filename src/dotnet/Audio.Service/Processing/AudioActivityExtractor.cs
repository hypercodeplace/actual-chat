namespace ActualChat.Audio.Processing;

public class AudioActivityExtractor
{
    private readonly ILoggerFactory _loggerFactory;

    public AudioActivityExtractor(ILoggerFactory loggerFactory)
        => _loggerFactory = loggerFactory;

#pragma warning disable CS1998
    public async IAsyncEnumerable<OpenAudioSegment> SplitToAudioSegments(
#pragma warning restore CS1998
        AudioRecord audioRecord,
        IAsyncEnumerable<BlobPart> blobStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // TODO(AY): Implement actual audio activity extractor
        var audioLog = _loggerFactory.CreateLogger<AudioSource>();
        var audio = new AudioSource(blobStream, TimeSpan.Zero, audioLog, cancellationToken);
        var openAudioSegment = new OpenAudioSegment(0, audioRecord, audio, TimeSpan.Zero, cancellationToken);
        _ = Task.Run(async () => {
            try {
                await audio.WhenFormatAvailable.ConfigureAwait(false);
                await audio.WhenDurationAvailable.ConfigureAwait(false);
                openAudioSegment.Close(audio.Duration);
            }
            catch (Exception error) {
                openAudioSegment.TryClose(error);
            }
        }, CancellationToken.None);
        yield return openAudioSegment;
    }
}
