using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class FlightExtensionMethods
{
    public static FlightMessage ToMessage(this Flight flight, Sequence sequence)
    {
        return new FlightMessage
        {
            Callsign = flight.Callsign,
            AircraftType = flight.AircraftType,
            WakeCategory = flight.WakeCategory,
            OriginIdentifier = flight.OriginIdentifier,
            DestinationIdentifier = flight.DestinationIdentifier,
            State = flight.State,
            NumberInSequence = sequence.NumberInSequence(flight),
            FeederFixIdentifier = flight.FeederFixIdentifier,
            InitialFeederFixEstimate = flight.InitialFeederFixTime,
            FeederFixEstimate = flight.EstimatedFeederFixTime,
            FeederFixTime = flight.ScheduledFeederFixTime,
            AssignedRunway = flight.AssignedRunwayIdentifier,
            NumberToLandOnRunway = sequence.NumberForRunway(flight),
            InitialLandingEstimate = flight.InitialLandingTime,
            LandingEstimate = flight.EstimatedLandingTime,
            LandingTime = flight.ScheduledLandingTime,
            InitialDelay = flight.TotalDelay,
            RemainingDelay = flight.RemainingDelay,
            FlowControls = flight.FlowControls
        };
    }
}
