﻿using ActualChat.Audio.Db;
using ActualChat.Audio.Processing;
using ActualChat.Hosting;
using ActualChat.Redis.Module;
using ActualChat.Transcription;
using ActualChat.Web.Module;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.Plugins;

namespace ActualChat.Audio.Module;

public class AudioModule : HostModule<AudioSettings>, IWebModule
{
    public AudioModule(IPluginInfoProvider.Query _) : base(_) { }

    [ServiceConstructor]
    public AudioModule(IPluginHost plugins) : base(plugins) { }

    public void ConfigureApp(IApplicationBuilder app)
        => app.UseEndpoints(endpoints => {
            endpoints.MapHub<AudioHub>("/api/hub/audio");
        });

    public override void InjectServices(IServiceCollection services)
    {
        base.InjectServices(services);
        if (!HostInfo.RequiredServiceScopes.Contains(ServiceScope.Server))
            return; // Server-side only module

        services.AddResponseCaching();

        // Redis
        var redisModule = Plugins.GetPlugins<RedisModule>().Single();
        redisModule.AddRedisDb<AudioContext>(services, Settings.Redis);

        // SignalR hub & related services
        var signalR = services.AddSignalR(options => {
            options.StreamBufferCapacity = 20;
            options.EnableDetailedErrors = true;
        });
        if (!Debugging.SignalR.DisableMessagePackProtocol)
            signalR.AddMessagePackProtocol();

        // Module's own services
        services.AddTransient<AudioHub>();

        services.TryAddSingleton<AudioProcessor.Options>();
        services.AddSingleton<AudioProcessor>();
        services.AddTransient<IAudioProcessor>(c => c.GetRequiredService<AudioProcessor>());

        services.AddSingleton<AudioSegmentSaver>();
        services.AddSingleton<AudioDownloader, LocalAudioDownloader>();
        services.AddSingleton<AudioStreamer>();
        services.AddTransient<IAudioStreamer>(c => c.GetRequiredService<AudioStreamer>());
        services.AddSingleton<TranscriptSplitter>();
        services.AddSingleton<TranscriptPostProcessor>();
        services.AddSingleton<TranscriptStreamer>();
        services.AddTransient<ITranscriptStreamer>(c => c.GetRequiredService<TranscriptStreamer>());
    }
}
