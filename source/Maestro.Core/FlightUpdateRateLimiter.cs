using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Microsoft.Extensions.Logging;

namespace Maestro.Core;

public interface IFlightUpdateRateLimiter
{
    bool ShouldUpdateFlight(Flight flight, FlightPosition position);
}

public class FlightUpdateRateLimiter(IFixLookup fixLookup, ILogger<FlightUpdateRateLimiter> logger, IClock clock)
    : IFlightUpdateRateLimiter
{
    public bool ShouldUpdateFlight(Flight flight, FlightPosition position)
    {
        Fix? referenceFix = null;
        if (flight.FeederFixIdentifier is not null)
        {
            referenceFix = fixLookup.FindFix(flight.FeederFixIdentifier);
        }

        referenceFix ??= fixLookup.FindFix(flight.DestinationIdentifier);
        if (referenceFix is null)
        {
            logger.LogWarning(
                "Unable to find fix for {Callsign}. Feeder Fix identifier is {FeederFixIdentifier}. Destination is {DestinationIdentifier}",
                flight.Callsign,
                flight.FeederFixIdentifier,
                flight.DestinationIdentifier);
            return false;
        }
        
        var distanceToPoint = Calculations.CalculateDistanceNauticalMiles(
            position.Coordinate,
            referenceFix.Coordinate);
        
        // TODO: Make these configurable
        var vsp = 150;
        var updateRateBeyondRange = TimeSpan.FromMinutes(1);
        var updateRateWithinRange = TimeSpan.FromSeconds(30);

        var updateRate = distanceToPoint > vsp ? updateRateBeyondRange : updateRateWithinRange;
        var shouldUpdate = clock.UtcNow() - flight.LastSeen >= updateRate;
        return shouldUpdate;
    }
}