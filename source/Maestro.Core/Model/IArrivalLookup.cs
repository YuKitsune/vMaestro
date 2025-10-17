using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

public interface IArrivalLookup
{
    public TimeSpan GetTimeToGo(Flight flight);
}

// TODO: Test cases:
// - No feeder fix, throws
// - Airport mismatch, no match
// - Feeder fix mismatch, no match
// - Runway mismatch, no match
// - Has transition fix, flight route missing it, no match
// - Has transition fix, flight route includes it, match
// - Category match, match
// - Specific aircraft type match, match
// - Approach type mismatch, no match
// - All criteria match, returns interval

public class ArrivalLookup(IArrivalConfigurationLookup arrivalConfigurationLookup) : IArrivalLookup
{
    public TimeSpan GetTimeToGo(Flight flight)
    {
        // TODO: Eventually, we might want to return the entire arrival as a domain type and store it on the flight.

        if (string.IsNullOrEmpty(flight.FeederFixIdentifier))
            throw new MaestroException("Cannot lookup arrival without a feeder fix");

        foreach (var arrival in arrivalConfigurationLookup.GetArrivals())
        {
            if (arrival.AirportIdentifier != flight.DestinationIdentifier)
                continue;

            if (arrival.FeederFixIdentifier != flight.FeederFixIdentifier)
                continue;

            if (arrival.RunwayIdentifier != flight.AssignedRunwayIdentifier)
                continue;

            // If this arrival has a transition fix, ensure the flight's route includes it
            if (!string.IsNullOrEmpty(arrival.TransitionFixIdentifier) && flight.Fixes.All(f => f.FixIdentifier != arrival.TransitionFixIdentifier))
                continue;

            // Match either by category or specific aircraft type
            if (arrival.Category != flight.AircraftCategory && !arrival.AircraftTypes.Contains(flight.AircraftType))
                continue;

            if (arrival.ApproachType != flight.ApproachType)
                continue;

            return arrival.TimeToGo;
        }

        throw new MaestroException($"No matching arrival found for flight {flight.Callsign} to {flight.DestinationIdentifier}/{flight.AssignedRunwayIdentifier} via {flight.FeederFixIdentifier}");
    }
}
