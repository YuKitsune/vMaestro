using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISeparationRuleProvider, SeparationRuleProvider>()
            .AddSingleton<ISequenceProvider, SequenceProvider>()
            .AddSingleton<IPerformanceLookup, PerformanceLookup>()
            .AddSingleton<IClock, SystemClock>();
    }
}
