using ActualChat.Audio.Module;
using ActualChat.Audio.UI.Blazor.Module;
using ActualChat.Chat.Module;
using ActualChat.Chat.UI.Blazor;
using ActualChat.Chat.UI.Blazor.Module;
using ActualChat.Db.Module;
using ActualChat.Host.Module;
using ActualChat.Hosting;
using ActualChat.MediaPlayback.Module;
using ActualChat.Module;
using ActualChat.Transcription.Module;
using ActualChat.UI.Blazor;
using ActualChat.UI.Blazor.Module;
using ActualChat.Users.Module;
using ActualChat.Users.UI.Blazor.Module;
using ActualChat.Web.Module;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Stl.Plugins;

namespace ActualChat.Host;

public class Startup
{
    private IConfiguration Cfg { get; }
    private IWebHostEnvironment Env { get; }
    private IPluginHost Plugins { get; set; } = null!;
    private ImmutableArray<HostModule> HostModules { get; set; } = ImmutableArray<HostModule>.Empty;
    private ILogger Log => Plugins?.GetService<ILogger<Startup>>() ?? NullLogger<Startup>.Instance;

    public Startup(IConfiguration cfg, IWebHostEnvironment environment)
    {
        Cfg = cfg;
        Env = environment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.AddConsole();
#pragma warning disable IL2026
            logging.AddConsoleFormatter<GoogleCloudConsoleFormatter, JsonConsoleFormatterOptions>();
#pragma warning restore IL2026
            logging.SetMinimumLevel(Env.IsDevelopment() ? LogLevel.Debug : LogLevel.Warning);
            // use appsettings*.json to configure logging filters
        });

        // HostInfo
        services.AddSingleton(new HostInfo() {
            HostKind = HostKind.WebServer,
            RequiredServiceScopes = ImmutableHashSet<Symbol>.Empty
                .Add(ServiceScope.Server)
                .Add(ServiceScope.BlazorUI),
            Environment = Env.EnvironmentName,
            Configuration = Cfg,
        });

        // Creating plugins & host modules
        var pluginServices = new ServiceCollection()
            .Add(services)
            .AddSingleton(Cfg)
            .AddSingleton(Env)
            .AddSingleton(new FileSystemPluginFinder.Options {
                AssemblyNamePattern = "ActualChat.*.dll",
            });

        // FileSystemPluginFinder cache fails on .NET 6 some times, so...
        /*
        var pluginHostBuilder = new PluginHostBuilder(pluginServices);
        pluginHostBuilder.UsePlugins(
            // Core modules
            typeof(CoreModule),
            typeof(PlaybackModule),
            typeof(DbModule),
            typeof(WebModule),
            typeof(BlazorUICoreModule),
            // Services
            typeof(AudioModule),
            typeof(AudioBlazorUIModule),
            typeof(TranscriptionModule),
            typeof(ChatModule),
            typeof(ChatBlazorUIModule),
            typeof(UsersModule),
            typeof(UsersBlazorUIModule),
            // "The rest of Startup.cs" module
            typeof(AppHostModule)
        );
        Plugins = pluginHostBuilder.Build();
        */
        Plugins = new PluginHostBuilder(pluginServices).Build();
        HostModules = Plugins
            .GetPlugins<HostModule>()
            .OrderBy(m => m is not AppHostModule) // MainHostModule should be the first one
            .ToImmutableArray();

        // Using host modules to inject the remaining services
        HostModules.Apply(m => m.InjectServices(services));
    }

    public void Configure(IApplicationBuilder app)
        => HostModules.OfType<IWebModule>().Apply(m => m.ConfigureApp(app));
}
