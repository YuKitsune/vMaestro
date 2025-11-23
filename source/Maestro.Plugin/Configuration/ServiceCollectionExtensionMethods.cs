using Maestro.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Plugin.Configuration;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, PluginConfiguration pluginConfiguration)
    {
        return services
            .AddSingleton(pluginConfiguration)
            .AddSingleton<ILoggingConfiguration>(pluginConfiguration.Logging)
            .AddSingleton<IAirportConfigurationProvider>(new AirportConfigurationProvider(pluginConfiguration.Airports))
            .AddSingleton<CoordinationMessageConfiguration>(pluginConfiguration.CoordinationMessages);
    }
}
