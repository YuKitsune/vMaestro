using Maestro.Core.Infrastructure;

namespace Maestro.Core;

public interface IFlightUpdateRateLimiter
{
    bool ShouldUpdate(DateTimeOffset lastSeen);
}

public class FlightUpdateRateLimiter(IClock clock)
    : IFlightUpdateRateLimiter
{
    public bool ShouldUpdate(DateTimeOffset lastSeen)
    {
        var updateRate = TimeSpan.FromSeconds(30);
        return clock.UtcNow() - lastSeen >= updateRate;
    }
}
