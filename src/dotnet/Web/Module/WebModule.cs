﻿using ActualChat.Hosting;
using ActualChat.Web.Internal;
using Microsoft.AspNetCore.Builder;
using Stl.Plugins;

namespace ActualChat.Web.Module;

public class WebModule : HostModule, IWebModule
{
    public WebModule(IPluginInfoProvider.Query _) : base(_) { }
    [ServiceConstructor]
    public WebModule(IPluginHost plugins) : base(plugins) { }

    public override void InjectServices(IServiceCollection services)
    {
        if (!HostInfo.RequiredServiceScopes.Contains(ServiceScope.Server))
            return; // Server-side only module

        services.AddMvcCore(options => {
            options.ModelBinderProviders.Insert(1, new IdentifierModelBinderProvider());
        });

    }

    public void ConfigureApp(IApplicationBuilder app)
    { }
}
