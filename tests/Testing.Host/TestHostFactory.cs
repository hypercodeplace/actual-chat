using ActualChat.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Stl.Fusion.Server.Authentication;
using Stl.IO;

namespace ActualChat.Testing.Host;

public static class TestHostFactory
{
    public static FilePath GetManifestPath()
    {
        static FilePath AssemblyPathToManifestPath(FilePath assemblyPath)
        {
            return assemblyPath.ChangeExtension("staticwebassets.runtime.json");
        }

        var hostAssemblyPath = (FilePath)typeof(AppHost).Assembly.Location;
        var manifestPath = AssemblyPathToManifestPath(hostAssemblyPath);
        if (File.Exists(manifestPath))
            return manifestPath;

        throw new FileNotFoundException("Can't find manifest.", manifestPath);
    }

    public static async Task<AppHost> NewAppHost(
        Action<AppHost>? configure = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var port = WebTestExt.GetUnusedTcpPort();
        var manifestPath = GetManifestPath();
        var appHost = new AppHost {
            ServerUrls = WebTestExt.GetLocalUri(port).ToString(),
            HostConfigurationBuilder = cfg => {
                cfg.Sources.Insert(0,
                    new MemoryConfigurationSource {
                        InitialData = new Dictionary<string, string> {
                            { WebHostDefaults.EnvironmentKey, "Development" },
                            { WebHostDefaults.StaticWebAssetsKey, manifestPath },
                        },
                    });
            },
            AppServicesBuilder = (_, services) => {
                configureServices?.Invoke(services);
                services.TryAddSingleton(new ServerAuthHelper.Options {
                    KeepSignedIn = true,
                });
            },
            AppConfigurationBuilder = GetTestAppSettings,
        };
        configure?.Invoke(appHost);
        await appHost.Build();
        await appHost.Initialize();
        await appHost.Start();
        return appHost;
    }

    private static void GetTestAppSettings(IConfigurationBuilder config)
    {
        var toDelete = config.Sources
            .Where(s => s is JsonConfigurationSource source
                && source.Path.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var source in toDelete)
            config.Sources.Remove(source);

        var fileProvider = new PhysicalFileProvider(Path.GetDirectoryName(typeof(TestSettings).Assembly.Location));
        foreach (var (fileName, optional) in GetTestSettingsFiles())
            config.Sources.Add(new JsonConfigurationSource {
                FileProvider = fileProvider,
                OnLoadException = null,
                Optional = optional,
                Path = fileName,
                ReloadDelay = 100,
                ReloadOnChange = false,
            });

        static List<(string FileName, bool Optional)> GetTestSettingsFiles()
        {
            List<(string FileName, bool Optional)> result = new (3) {
                ("testsettings.json", Optional: false),
                ("testsettings.local.json", Optional: true),
            };
            if (bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                    out var isRunningContainer)
                && isRunningContainer)
                result.Add(("testsettings.docker.json", Optional: true));
            return result;
        }
    }
}
