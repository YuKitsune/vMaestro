using Microsoft.Extensions.DependencyInjection;
using TFMS.Core.Configuration;

namespace TFMS.Plugin.Configuration;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection WithPluginConfigurationSource(this IServiceCollection services)
    {
        return services
            .AddSingleton<ILoggingConfigurationProvider, PluginConfigurationProvider>()
            .AddSingleton<IAirportConfigurationProvider, PluginConfigurationProvider>();
    }
}
