using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ActualChat.Audio.Orchestration;
using ActualChat.Blobs;
using ActualChat.Distribution;
using ActualChat.Transcription;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stl.Fusion.Authentication;
using Stl.Text;

namespace ActualChat.Audio
{
    public class AudioOrchestrator : BackgroundService
    {
        private readonly IAuthService _authService;
        private readonly IBlobStorageProvider _blobStorageProvider;
        private readonly ITranscriber _transcriber;
        private readonly IServerSideAudioStreamingService _streamingService;
        private readonly IServerSideStreamingService<TranscriptMessage> _transcriptStreamingService;
        private readonly ILogger<AudioOrchestrator> _log;

        public AudioOrchestrator(
            IAuthService authService,
            IBlobStorageProvider blobStorageProvider,
            ITranscriber transcriber,
            IServerSideAudioStreamingService streamingService,
            IServerSideStreamingService<TranscriptMessage> transcriptStreamingService,
            ILogger<AudioOrchestrator> log)
        {
            _authService = authService;
            _blobStorageProvider = blobStorageProvider;
            _transcriber = transcriber;
            _streamingService = streamingService;
            _transcriptStreamingService = transcriptStreamingService;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true) {
                // TODO(AK): add push-back based on current node performance metrics \ or provide signals for scale-out 
                var recording = await WaitForNewRecording(stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                    return;

                _ = StartAudioPipeline(recording!, stoppingToken);
            }
        }

        private async Task<AudioRecording?> WaitForNewRecording(CancellationToken cancellationToken)
        {
            var recording = await _streamingService.WaitForNewRecording(cancellationToken);
            while (recording == null && !cancellationToken.IsCancellationRequested) 
                recording = await _streamingService.WaitForNewRecording(cancellationToken);
            
            return recording;
        }

        private async Task StartAudioPipeline(AudioRecording recording, CancellationToken cancellationToken)
        {
            var audioReader = await _streamingService.GetStream(recording.Id, cancellationToken);
            await foreach (var audioStreamEntry in SplitStreamBySilencePeriods(recording, audioReader, cancellationToken)) {
                var distributeStreamTask = DistributeAudioStream(audioStreamEntry, cancellationToken);
                var chatEntryTask = PublishChatEntry(audioStreamEntry, cancellationToken);

                var transcriptionResults = await Transcribe(audioStreamEntry, cancellationToken);
                _ = DistributeTranscriptionResults(transcriptionResults, cancellationToken);

                _ = PersistStreamEntry(audioStreamEntry, cancellationToken);

                await Task.WhenAll(distributeStreamTask, chatEntryTask);
            }

        }

        private Task PersistStreamEntry(AudioStreamEntry audioStreamEntry, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private Task DistributeTranscriptionResults(
            IAsyncEnumerable<TranscriptMessage> transcriptionResults,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async IAsyncEnumerable<TranscriptMessage> Transcribe(AudioStreamEntry audioStreamEntry, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var (recordingId, configuration) = audioStreamEntry.AudioRecording;
            // TODO: AK - read actual config
            var command = new BeginTranscriptionCommand {
                RecordingId = recordingId.Value,
                AudioFormat = new AudioFormat {
                    Codec = AudioCodec.Opus,
                    ChannelCount = 1,
                    SampleRate = 48_000
                },
                Options = new TranscriptionOptions {
                    Language = "ru-RU",
                    IsDiarizationEnabled = false,
                    IsPunctuationEnabled = true,
                    MaxSpeakerCount = 1
                }
            };
            var transcriptId = await _transcriber.BeginTranscription(command, cancellationToken);

            var reader = audioStreamEntry.GetStream();
            _ = PushAudioStreamForTranscription(transcriptId, reader, cancellationToken);

            var index = 0;
            var result = await _transcriber.PollTranscription(new PollTranscriptionCommand(transcriptId, index), cancellationToken);
            while (result.ContinuePolling && !cancellationToken.IsCancellationRequested) {
                foreach (var fragmentVariant in result.Fragments) {
                    if (fragmentVariant.Speech is { } speechFragment) {
                        var message = new TranscriptMessage(
                            speechFragment.Text,
                            speechFragment.TextIndex,
                            speechFragment.StartOffset,
                            speechFragment.Duration);
                        yield return message;
                    }
                    else if (fragmentVariant.Error != null) {
                        // TODO(AK) - think about additional scenarios of transcription error handling
                    }

                    index = fragmentVariant.Value!.Index + 1;
                }

                result = await _transcriber.PollTranscription(
                    new PollTranscriptionCommand(transcriptId, index),
                    cancellationToken);
            }

            await _transcriber.AckTranscription(new AckTranscriptionCommand(transcriptId, index), cancellationToken);

            async Task PushAudioStreamForTranscription(Symbol tId, ChannelReader<AudioMessage> r, CancellationToken ct)
            {
                await foreach (var (_,_, chunk) in r.ReadAllAsync(ct)) {
                    var appendCommand = new AppendTranscriptionCommand(tId, chunk);
                    await _transcriber.AppendTranscription(appendCommand, ct);
                }

                await _transcriber.EndTranscription(new EndTranscriptionCommand(tId), ct);
            }
        }

        private Task PublishChatEntry(AudioStreamEntry audioStreamEntry, CancellationToken cancellationToken)
        {
            // TODO(AK): Implement creating of a chat entry
            return Task.CompletedTask;
        }

        private Task DistributeAudioStream(AudioStreamEntry audioStreamEntry, CancellationToken cancellationToken) 
            => _streamingService.PublishStream(audioStreamEntry.StreamId, audioStreamEntry.GetStream(), cancellationToken);

        private IAsyncEnumerable<AudioStreamEntry> SplitStreamBySilencePeriods(
            AudioRecording audioRecording,
            ChannelReader<AudioMessage> audioReader,
            CancellationToken cancellationToken)
        {
            var splitter = new AudioStreamSplitter();
            return splitter.SplitBySilencePeriods(audioRecording, audioReader, cancellationToken);
        }
    }
}