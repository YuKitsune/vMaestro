using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Wpf;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddViewModels(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<MaestroViewModel>();
    }
}
