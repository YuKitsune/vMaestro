using Maestro.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Plugin.Configuration;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, PluginConfigurationV2 pluginConfiguration)
    {
        return services
            .AddSingleton(pluginConfiguration)
            .AddSingleton<ILoggingConfiguration>(pluginConfiguration.Logging)
            .AddSingleton<IAirportConfigurationProviderV2>(new AirportConfigurationProviderV2(pluginConfiguration.Airports))
            .AddSingleton(pluginConfiguration.Labels);
    }
}
