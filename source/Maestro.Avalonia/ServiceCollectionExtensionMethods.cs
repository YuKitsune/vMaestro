using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Avalonia;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddViewModels(this IServiceCollection serviceCollection)
    {
        return serviceCollection;
    }
}
