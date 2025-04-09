using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static SequenceDto ToDto(this Sequence sequence)
    {
        return new SequenceDto(
            sequence.AirportIdentifier,
            sequence.Flights.Select(x =>
                new FlightDto(
                    x.Callsign,
                    x.AircraftType,
                    x.WakeCategory,
                    x.OriginIdentifier,
                    x.DestinationIdentifier,
                    x.State,
                    x.FeederFixIdentifier,
                    x.AssignedRunwayIdentifier,
                    x.AssignedStarIdentifier,
                    x.InitialFeederFixTime,
                    x.EstimatedFeederFixTime,
                    x.ScheduledFeederFixTime,
                    x.InitialLandingTime,
                    x.EstimatedLandingTime,
                    x.ScheduledLandingTime,
                    x.TotalDelayToRunway,
                    x.RemainingDelayToRunway))
            .ToArray());

    }
}
