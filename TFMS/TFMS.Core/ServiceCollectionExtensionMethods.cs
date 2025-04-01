using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TFMS.Core.Configuration;
using TFMS.Core.Model;

namespace TFMS.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<SequenceProvider>();
    }

    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfigurationProvider initialConfigurationProvider)
    {
        return services.AddSingleton<IConfigurationProvider>(c =>
            new SwapableConfigurationProvider(c.GetRequiredService<IMediator>(), initialConfigurationProvider));
    }
}
