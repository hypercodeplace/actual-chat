﻿using ActualChat.Audio.UI.Blazor.Module;
using ActualChat.Messaging;

namespace ActualChat.Audio.UI.Blazor.Components;

public record AudioRecorderState(Symbol ChatId, bool IsRecording)
{
    public Ulid Id { get; init; } = Ulid.NewUlid();
}

public class AudioRecorder : IAudioRecorderBackend, IAsyncDisposable
{
    private readonly ILogger<AudioRecorder> _log;
    private ILogger? DebugLog => DebugMode ? _log : null;
    private bool DebugMode => Constants.DebugMode.AudioRecording;

    private readonly Session _session;
    private readonly IJSRuntime _js;
    private readonly IMessageProcessor<IAudioRecorderCommand> _messageProcessor;
    private IJSObjectReference? JSRef { get; set; }
    private DotNetObjectReference<IAudioRecorderBackend>? BlazorRef { get; set; }

    public Task WhenInitialized { get; }
    public IMutableState<AudioRecorderState?> State { get; }
    public bool IsRecordingStarted => State.ValueOrDefault != null;
    public bool IsRecording => State.ValueOrDefault is { IsRecording: true };

    public AudioRecorder(
        ILogger<AudioRecorder> log,
        Session session,
        IStateFactory stateFactory,
        IJSRuntime js)
    {
        _log = log;
        _session = session;
        _js = js;
        _messageProcessor = new MessageProcessor<IAudioRecorderCommand>(ProcessCommand);
        State = stateFactory.NewMutable<AudioRecorderState?>();
        WhenInitialized = Initialize();

        async Task Initialize()
        {
            BlazorRef = DotNetObjectReference.Create<IAudioRecorderBackend>(this);
            JSRef = await _js.InvokeAsync<IJSObjectReference>(
                $"{AudioBlazorUIModule.ImportName}.AudioRecorder.create",
                BlazorRef, _session.Id).ConfigureAwait(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProcessor.Complete().SuppressExceptions().ConfigureAwait(false);
        await _messageProcessor.DisposeAsync().ConfigureAwait(false);
        await StopRecordingInternal().ConfigureAwait(true);
        await JSRef.DisposeSilentlyAsync().ConfigureAwait(true);
        // ReSharper disable once ConstantConditionalAccessQualifier
        BlazorRef?.Dispose();
    }

    public IMessageProcess<StartAudioRecorderCommand> StartRecording(Symbol chatId, CancellationToken cancellationToken = default)
    {
        if (chatId.IsEmpty)
            throw new ArgumentOutOfRangeException(nameof(chatId));
        return _messageProcessor.Enqueue(new StartAudioRecorderCommand(chatId), cancellationToken);
    }

    public IMessageProcess<StopAudioRecorderCommand> StopRecording()
        => _messageProcessor.Enqueue(StopAudioRecorderCommand.Instance);

    private async Task<object?> ProcessCommand(IAudioRecorderCommand command, CancellationToken cancellationToken)
    {
        switch (command) {
        case StartAudioRecorderCommand startCommand:
            var chatId = startCommand.ChatId;
            await WhenInitialized.ConfigureAwait(false);
            await StartRecordingInternal(chatId).ConfigureAwait(false);
            break;
        case StopAudioRecorderCommand:
            if (!WhenInitialized.IsCompletedSuccessfully)
                throw new LifetimeException("Recorder is not initialized yet.");
            await StopRecordingInternal().ConfigureAwait(false);
            break;
        default:
            throw new NotSupportedException($"Unsupported command type: '{command.GetType()}'.");
        }
        return null;
    }

    private async Task StartRecordingInternal(string chatId)
    {
        if (IsRecordingStarted) return;
        DebugLog?.LogDebug(nameof(StartRecordingInternal) + ": chat #{ChatId}", chatId);

        State.Value = new AudioRecorderState(chatId, false);
        if (JSRef != null)
            await JSRef.InvokeVoidAsync("startRecording", chatId).ConfigureAwait(false);
    }

    private async Task StopRecordingInternal()
    {
        if (!IsRecordingStarted) return;
        DebugLog?.LogDebug(nameof(StopRecordingInternal));

        var state = State.Value;
        _ = Task.Delay(TimeSpan.FromSeconds(5))
            .ContinueWith(async _ => {
                if (State.Value?.Id != state?.Id)
                    return; // We don't want to stop the next recording here

                _log.LogWarning(nameof(OnRecordingStopped) + " wasn't invoked on time by _js backend");
                await OnRecordingStopped().ConfigureAwait(true);
            }, TaskScheduler.Current);

        if (JSRef != null)
            await JSRef.InvokeVoidAsync("stopRecording").ConfigureAwait(false);
    }

    // JS backend callback handlers

    [JSInvokable]
    public void OnRecordingStarted(string chatId)
    {
        if (!IsRecordingStarted) return;
        DebugLog?.LogDebug(nameof(OnRecordingStarted) + ": chat #{ChatId}", chatId);

        State.Value = (State.Value ?? new AudioRecorderState(chatId, true)) with { IsRecording = true };
    }

    [JSInvokable]
    public Task OnRecordingStopped()
    {
        // Does the same as StopRecording; we assume here that recording
        // might be recognized as stopped by JS backend as well
        if (!IsRecordingStarted) return Task.CompletedTask;
        DebugLog?.LogDebug(nameof(OnRecordingStopped));

        State.Value = null;
        return Task.CompletedTask;
    }
}
