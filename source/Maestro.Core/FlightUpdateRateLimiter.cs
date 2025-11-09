using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core;

public interface IFlightUpdateRateLimiter
{
    bool ShouldUpdateFlight(Flight flight);
}

public class FlightUpdateRateLimiter(IClock clock)
    : IFlightUpdateRateLimiter
{
    public bool ShouldUpdateFlight(Flight flight)
    {
        var updateRate = TimeSpan.FromSeconds(30);
        var shouldUpdate = clock.UtcNow() - flight.LastSeen >= updateRate;
        return shouldUpdate;
    }
}
