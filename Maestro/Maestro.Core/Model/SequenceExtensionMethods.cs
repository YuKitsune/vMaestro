using Maestro.Core.Dtos;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static SequenceDTO ToDTO(this Sequence sequence)
    {
        return new SequenceDTO(
            sequence.AirportIdentifier,
            sequence.Arrivals.Select(x =>
                new FlightDTO(
                    x.Callsign,
                    x.AircraftType,
                    x.OriginIcaoCode,
                    x.DestinationIcaoCode,
                    x.State.ToDTO(),
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
