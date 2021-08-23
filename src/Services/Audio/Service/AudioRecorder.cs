﻿using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ActualChat.Audio.Db;
using ActualChat.Audio.WebM;
using ActualChat.Audio.WebM.Models;
using ActualChat.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Stl.Async;
using Stl.Fusion;
using Stl.Fusion.Authentication;
using Stl.Fusion.EntityFramework;
using Stl.Generators;
using Stl.Text;
using Stl.Time;

namespace ActualChat.Audio
{
    public class AudioRecorder : DbServiceBase<AudioDbContext>, IAudioRecorder
    {
        private static readonly TimeSpan SegmentLength = new TimeSpan(0, 0, 10);
        private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();

        private readonly IAuthService _authService;
        private readonly IBlobStorageProvider _blobStorageProvider;
        private readonly ILogger<AudioRecorder> _log;

        private readonly Generator<string> _idGenerator;
        private readonly ConcurrentDictionary<Symbol, (Channel<AppendAudioCommand>, Task)> _dataCapacitor;
        

        public AudioRecorder(IServiceProvider services, IAuthService authService, IBlobStorageProvider blobStorageProvider, ILogger<AudioRecorder> log) : base(services)
        {
            _authService = authService;
            _blobStorageProvider = blobStorageProvider;
            _log = log;
            _idGenerator = new RandomStringGenerator(16, RandomStringGenerator.Base32Alphabet);
            _dataCapacitor = new ConcurrentDictionary<Symbol, (Channel<AppendAudioCommand>, Task)>();
        }

        public virtual async Task<Symbol> Initialize(InitializeAudioRecorderCommand command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return default!;

            var user = await _authService.GetUser(command.Session, cancellationToken);
            user.MustBeAuthenticated();

            await using var dbContext = await CreateCommandDbContext(cancellationToken);

            var recordingId = _idGenerator.Next();
            _log.LogInformation($"{nameof(Initialize)}, RecordingId = {{RecordingId}}", recordingId);
            dbContext.AudioRecordings.Add(new DbAudioRecording {
                Id = recordingId,
                UserId = user.Id,
                RecordingStartedUtc = command.ClientStartOffset,
                AudioCodec = command.AudioFormat.Codec,
                ChannelCount = command.AudioFormat.ChannelCount,
                SampleRate = command.AudioFormat.SampleRate,
                Language = command.Language,
                RecordingDuration = 0
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            
            var channel = Channel.CreateUnbounded<AppendAudioCommand>(new UnboundedChannelOptions{ SingleReader = true });
            _dataCapacitor.TryAdd(recordingId, (channel, FlushBufferedSegments(recordingId, command.ClientStartOffset, channel)));

            return recordingId;
        }

        public virtual async Task AppendAudio(AppendAudioCommand command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return;

            var (recordingId, index, offset, data) = command;
            _log.LogTrace($"{nameof(AppendAudio)}, RecordingId = {{RecordingId}}, Index = {{Index}}, DataLength = {{DataLength}}",
                recordingId.Value,
                command.Index,
                command.Data.Count);

            // Push to real-time pipeline
            // TBD

            // Waiting for Initialize
            var waitAttempts = 0;
            while (!_dataCapacitor.ContainsKey(recordingId) && waitAttempts < 5) {
                await Task.Delay(10, cancellationToken);
                waitAttempts++;
            }
            
            // Initialize hasn't been completed or Recording has already been completed
            if (!_dataCapacitor.TryGetValue(recordingId, out var tuple)) return;

            var (channel, _) = tuple;
            await channel.Writer.WriteAsync(command, cancellationToken);;
        }

        public virtual async Task Complete(CompleteAudioRecording command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return;

            if (_dataCapacitor.TryRemove(command.RecordingId, out var tuple)) {
                var (channel, flushTask) = tuple;
                channel.Writer.Complete();
                await flushTask.WithFakeCancellation(cancellationToken);
            }
        }

        private async Task FlushBufferedSegments(
            Symbol recordingId, 
            Moment recordingStartOffset,
            Channel<AppendAudioCommand> channel)
        {
            var lastOffset = recordingStartOffset.ToUnixEpoch();
            var metaData = new List<SegmentMetaDataEntry>();
            EBML? ebml = null; 
            Segment? segment = null;
            WebMReader.State readerState = new WebMReader.State();
            var clusters = new List<Cluster>();
            var currentSegmentDuration = 0d;
            var lastIndex = 0;
            using var bufferLease = MemoryPool<byte>.Shared.Rent(32 * 1024);
            await foreach (var (_, index, clientEndOffset, base64Encoded) in channel.Reader.ReadAllAsync()) {
                var currentOffset = clientEndOffset.ToUnixEpoch();
                var duration = currentOffset - lastOffset;
                metaData.Add(new SegmentMetaDataEntry(index, lastOffset, duration));
                var remainingLength = readerState.Remaining;
                var buffer = bufferLease.Memory;
                // TODO: AK - check length!!!
                
                buffer.Slice(readerState.Position,remainingLength).CopyTo(buffer[..remainingLength]);
                base64Encoded.Data.CopyTo(buffer[readerState.Remaining..]);

                var dataLength = readerState.Remaining + base64Encoded.Count;
                
                var (cs, s) = ReadClusters(
                    readerState.IsEmpty 
                        ? new WebMReader(bufferLease.Memory.Span[..dataLength]) 
                        : WebMReader.FromState(readerState).WithNewSource(bufferLease.Memory.Span[..dataLength]));
                readerState = s;
                clusters.AddRange(cs);
                currentSegmentDuration += duration;

                if (currentSegmentDuration >= SegmentLength.TotalSeconds && clusters.Count > 0) {
                    currentSegmentDuration = 0;
                    await FlushSegment(recordingId, metaData, new WebMDocument(ebml!, segment!, clusters), index, lastOffset);
                    clusters.Clear();
                }
                
                lastOffset = currentOffset;
                lastIndex = index;
                
                (IReadOnlyList<Cluster>,WebMReader.State) ReadClusters(WebMReader reader)
                {
                    var result = new List<Cluster>(1);
                    while (reader.Read())
                        switch (reader.EbmlEntryType) {
                            case EbmlEntryType.None:
                                throw new InvalidOperationException();
                            case EbmlEntryType.Ebml:
                                // TODO: add support of EBML Stream where multiple headers and segments can appear
                                ebml = (EBML?)reader.Entry;
                                break;
                            case EbmlEntryType.Segment:
                                segment = (Segment?)reader.Entry;
                                break;
                            case EbmlEntryType.Cluster:
                                result.Add((Cluster)reader.Entry);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                    return (result, reader.GetState());
                }
            }
            
            if (readerState.Container is Cluster c) clusters.Add(c);

            await FlushSegment(recordingId, metaData, new WebMDocument(ebml!, segment!, clusters), ++lastIndex, lastOffset);
        }

        private async Task FlushSegment(Symbol recordingId, IReadOnlyList<SegmentMetaDataEntry> metaData, WebMDocument webM, int index, double offset)
        {
            if (metaData == null) throw new ArgumentNullException(nameof(metaData));
            if (webM == null) throw new ArgumentNullException(nameof(webM));
            var (ebml, segment, clusters) = webM;
            if (ebml == null) throw new ArgumentNullException(nameof(ebml));
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (clusters == null) throw new ArgumentNullException(nameof(clusters));

            var minBufferSize = 32*1024;
            var blobId = $"{BlobScope.AudioRecording}{BlobId.ScopeDelimiter}{recordingId}{BlobId.ScopeDelimiter}{Ulid.NewUlid().ToString()}";
            var blobStorage = _blobStorageProvider.GetBlobStorage(BlobScope.AudioRecording);
            await using var stream = MemoryStreamManager.GetStream(nameof(AudioRecorder));
            using var bufferLease = MemoryPool<byte>.Shared.Rent(minBufferSize);

            var ebmlWritten = WriteEntry(new WebMWriter(bufferLease.Memory.Span), ebml);
            var segmentWritten = WriteEntry(new WebMWriter(bufferLease.Memory.Span), segment);
            if (!ebmlWritten)
                throw new InvalidOperationException("Can't write EBML entry");
            if (!segmentWritten)
                throw new InvalidOperationException("Can't write Segment entry");

            foreach (var cluster in clusters) {
                var memory = bufferLease.Memory;
                var clusterWritten = WriteEntry(new WebMWriter(memory.Span), cluster);
                if (clusterWritten) continue;
                
                var cycleNumber = 0;
                var bufferSize = minBufferSize;
                while (true) {
                    bufferSize *= 2;
                    cycleNumber++;
                    using var extendedBufferLease = MemoryPool<byte>.Shared.Rent(bufferSize);
                    if (WriteEntry(new WebMWriter(extendedBufferLease.Memory.Span), cluster))
                        break;
                    if (cycleNumber >= 10)
                        break;
                }
            }

            stream.Position = 0;
            var persistBlob = blobStorage.WriteAsync(blobId, stream);
            var persistSegment = PersistSegment();

            bool WriteEntry(WebMWriter writer, RootEntry entry)
            {
                if (!writer.Write(entry))
                    return false;
                
                stream?.Write(writer.Written);
                return true;
            }

            async Task PersistSegment()
            {
                await using var dbContext = CreateDbContext(true);
                
                _log.LogInformation($"{nameof(FlushBufferedSegments)}, RecordingId = {{RecordingId}}", recordingId);
                dbContext.AudioSegments.Add(new DbAudioSegment {
                    RecordingId = recordingId,
                    Index = index,
                    Offset = offset,
                    Duration = metaData.Sum(md => md.Duration),
                    BlobId = blobId,
                    BlobMetadata = JsonSerializer.Serialize(metaData)
                });
                await dbContext.SaveChangesAsync();
            }

            await Task.WhenAll(persistBlob, persistSegment);
        }

        private record SegmentMetaDataEntry(int Index, double Offset, double Duration);

    }
}
