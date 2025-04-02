using Maestro.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Plugin.Configuration;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection WithPluginConfigurationSource(this IServiceCollection services)
    {
        return services
            .AddSingleton<ILoggingConfigurationProvider, PluginConfigurationProvider>()
            .AddSingleton<IAirportConfigurationProvider, PluginConfigurationProvider>();
    }
}
