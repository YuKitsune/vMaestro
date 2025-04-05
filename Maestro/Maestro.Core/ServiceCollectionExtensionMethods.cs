using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<SequenceProvider>()
            .AddSingleton<IClock, SystemClock>();
    }
}
