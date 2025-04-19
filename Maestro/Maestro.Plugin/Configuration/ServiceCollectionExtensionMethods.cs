using Maestro.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Plugin.Configuration;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration rootConfiguration)
    {
        return services
            .AddSingleton<ILoggingConfiguration>(new LoggingConfiguration(rootConfiguration.GetRequiredSection("Logging")))
            .AddSingleton<IMaestroConfiguration>(new MaestroConfiguration(rootConfiguration.GetRequiredSection("Maestro")))
            .AddSingleton<IAirportConfigurationProvider>(new AirportConfigurationProvider(rootConfiguration.GetRequiredSection("Airports").Get<AirportConfiguration[]>() ?? []));
    }
}
