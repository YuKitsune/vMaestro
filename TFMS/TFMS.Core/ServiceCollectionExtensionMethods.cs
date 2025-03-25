using Microsoft.Extensions.DependencyInjection;
using TFMS.Core.Model;

namespace TFMS.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<SequenceProvider>();
    }
}
