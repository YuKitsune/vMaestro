using Microsoft.Extensions.DependencyInjection;

namespace TFMS.Wpf;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddViewModels(this IServiceCollection serviceCollection)
    {
        return serviceCollection
            .AddSingleton<TFMSViewModel>();
    }
}
