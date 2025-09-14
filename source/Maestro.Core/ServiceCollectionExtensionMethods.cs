using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Services;
using Maestro.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISessionManager, SessionManager>()
            .AddSingleton<ISessionFactory, SessionFactory>()
            .AddSingleton<IScheduler, Scheduler>()
            .AddSingleton<IFlightUpdateRateLimiter, FlightUpdateRateLimiter>()
            .AddSingleton<IArrivalLookup, ArrivalLookup>()
            .AddSingleton<IEstimateProvider, EstimateProvider>()
            .AddSingleton<IRunwayScoreCalculator, RunwayScoreCalculator>()
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<IPermissionService, PermissionService>()
            .AddScoped<SequenceCleaner>();
    }
}
