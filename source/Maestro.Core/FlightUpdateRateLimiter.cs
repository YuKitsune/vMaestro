using Maestro.Core.Infrastructure;

namespace Maestro.Core;

public interface IFlightUpdateRateLimiter
{
    bool ShouldUpdateFlight(DateTimeOffset lastSeen);
}

public class FlightUpdateRateLimiter(IClock clock)
    : IFlightUpdateRateLimiter
{
    public bool ShouldUpdateFlight(DateTimeOffset lastSeen)
    {
        var updateRate = TimeSpan.FromSeconds(30);
        var shouldUpdate = clock.UtcNow() - lastSeen >= updateRate;
        return shouldUpdate;
    }
}
