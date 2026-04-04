using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Core;

public static class ServiceCollectionExtensionMethods
{
    public static IServiceCollection AddMaestro(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISessionManager, SessionManager>()
            .AddSingleton<IMaestroConnectionManager, MaestroConnectionManager>()
            .AddSingleton<IFlightUpdateRateLimiter, FlightUpdateRateLimiter>()
            .AddSingleton<ITrajectoryService, TrajectoryService>()
            .AddSingleton<IClock, SystemClock>();
    }
}
