using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<IArrivalLookup, ArrivalLookup>()
            .AddSingleton<IEstimateProvider, EstimateProvider>()
            .AddSingleton<ISeparationRuleProvider, SeparationRuleProvider>()
            .AddSingleton<ISequenceProvider, SequenceProvider>()
            .AddSingleton<IScheduler, Scheduler>()
            .AddSingleton<IClock, SystemClock>();
    }
}
