using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static SequenceDto ToDto(this Sequence sequence)
    {
        return new SequenceDto(
            sequence.AirportIdentifier,
            sequence.Arrivals.Select(x =>
                new FlightDto(
                    x.Callsign,
                    x.AircraftType,
                    x.OriginIcaoCode,
                    x.DestinationIcaoCode,
                    x.State.ToDto(),
                    x.FeederFix,
                    x.AssignedRunway,
                    x.AssignedStar,
                    x.InitialFeederFixTime,
                    x.EstimatedFeederFixTime,
                    x.ScheduledFeederFixTime,
                    x.InitialLandingTime,
                    x.EstimatedLandingTime,
                    x.ScheduledLandingTime))
            .ToArray());

    }
}
