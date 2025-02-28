using ActualChat.Hosting;
using ActualChat.Users;
using Stl.Fusion.Client;
using Stl.Plugins;

namespace ActualChat.Chat.Client.Module;

public class ChatClientModule : HostModule
{
    public ChatClientModule(IPluginInfoProvider.Query _) : base(_) { }

    [ServiceConstructor]
    public ChatClientModule(IPluginHost plugins) : base(plugins) { }

    public override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.RequiredServiceScopes.Contains(ServiceScope.Client))
            return; // Client-side only module

        var fusionClient = services.AddFusion().AddRestEaseClient();
        fusionClient.AddReplicaService<IChats, IChatsClientDef>();
        fusionClient.AddReplicaService<IChatAuthors, IChatAuthorsClientDef>();
        fusionClient.AddReplicaService<IChatUserSettings, IChatUserSettingsClientDef>();
        fusionClient.AddReplicaService<IUserAuthors, IUserAuthorsClientDef>();
        fusionClient.AddReplicaService<IInviteCodes, IInviteCodesClientDef>();
        fusionClient.AddReplicaService<IUserContacts, IUserContactsClientDef>();
    }
}
