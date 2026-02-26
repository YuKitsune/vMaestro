using Maestro.Core.Connectivity;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<IMaestroInstanceManager, MaestroInstanceManager>()
            .AddSingleton<IMaestroConnectionManager, MaestroConnectionManager>()
            .AddSingleton<IFlightUpdateRateLimiter, FlightUpdateRateLimiter>()
            .AddSingleton<IArrivalLookup, ArrivalLookup>()
            .AddSingleton<ITrajectoryService, TrajectoryService>()
            .AddSingleton<IClock, SystemClock>();
    }
}
