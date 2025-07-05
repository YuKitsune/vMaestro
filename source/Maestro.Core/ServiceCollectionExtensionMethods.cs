using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<IFlightUpdateRateLimiter, FlightUpdateRateLimiter>()
            .AddSingleton<IArrivalLookup, ArrivalLookup>()
            .AddSingleton<IEstimateProvider, EstimateProvider>()
            .AddSingleton<ISequenceProvider, SequenceProvider>()
            .AddSingleton<IScheduler, Scheduler>()
            .AddSingleton<SchedulerBackgroundService>()
            .AddSingleton<IRunwayAssigner, RunwayAssigner>()
            .AddSingleton<IClock, SystemClock>()
            .AddScoped<SequenceCleaner>();
    }
}
