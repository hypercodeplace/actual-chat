﻿using ActualChat.Hosting;
using ActualChat.Notification.Backend;
using Stl.Fusion.Client;
using Stl.Plugins;

namespace ActualChat.Notification.Client.Module;

public class NotificationClientModule : HostModule
{
    public NotificationClientModule(IPluginInfoProvider.Query _) : base(_) { }
    [ServiceConstructor]
    public NotificationClientModule(IPluginHost plugins) : base(plugins) { }

    public override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.RequiredServiceScopes.Contains(ServiceScope.Client))
            return; // Client-side only module

        var fusionClient = services.AddFusion().AddRestEaseClient();
        fusionClient.AddReplicaService<INotifications, INotificationsClientDef>();
        fusionClient.AddReplicaService<INotificationsBackend, INotificationsBackendClientDef>();
    }
}
