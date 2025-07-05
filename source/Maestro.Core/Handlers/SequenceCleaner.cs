using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core.Handlers;

public class SequenceCleaner(IClock clock, ILogger logger)
{
    // TODO: Make configurable
    readonly int _maxLandedFlights = 5;
    readonly TimeSpan _lostFlightTimeout = TimeSpan.FromHours(1);
    readonly TimeSpan _landedFlightTimeout = TimeSpan.FromMinutes(15);
    
    public void CleanUpFlights(Sequence sequence)
    {
        var landedFlights = sequence.Flights
            .Where(f => f.State == State.Landed)
            .ToArray();
        
        var flightsToTake = landedFlights.Length - _maxLandedFlights;
        if (flightsToTake > 0)
        {
            foreach (var landedFlight in landedFlights.Take(flightsToTake))
            {
                logger.Information(
                    "Deleting {Callsign} from {AirportIdentifier} as it has landed.", 
                    landedFlight.Callsign,
                    sequence.AirportIdentifier);
                sequence.Delete(landedFlight);
            }
        }
        
        var now = clock.UtcNow();
        var expiredLandedFlights = landedFlights
            .Where(f => now - f.ScheduledLandingTime >= _landedFlightTimeout)
            .ToArray();
        
        foreach (var landedFlight in expiredLandedFlights)
        {
            logger.Information(
                "Deleting {Callsign} from {AirportIdentifier} as it landed {Duration} ago.", 
                landedFlight.Callsign, 
                sequence.AirportIdentifier, 
                _lostFlightTimeout.ToHoursAndMinutesString());
            sequence.Delete(landedFlight);
        }
        
        var lostFlights = sequence.Flights
            .Where(f => now - f.LastSeen >= _lostFlightTimeout)
            .ToArray();
        
        foreach (var lostFlight in lostFlights)
        {
            logger.Information(
                "Deleting {Callsign} from {AirportIdentifier} as it has not been seen for {Duration}.", 
                lostFlight.Callsign, 
                sequence.AirportIdentifier, 
                _lostFlightTimeout.ToHoursAndMinutesString());
            sequence.Delete(lostFlight);
        }
    }
}