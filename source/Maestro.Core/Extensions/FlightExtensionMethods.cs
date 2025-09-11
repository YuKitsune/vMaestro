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
            EstimatedDepartureTime = flight.EstimatedDepartureTime,
            DestinationIdentifier = flight.DestinationIdentifier,
            IsFromDepartureAirport = flight.IsFromDepartureAirport,
            State = flight.State,
            NumberInSequence = sequence.NumberInSequence(flight),
            FeederFixIdentifier = flight.FeederFixIdentifier,
            InitialFeederFixEstimate = flight.InitialFeederFixEstimate,
            FeederFixEstimate = flight.FeederFixEstimate,
            FeederFixTime = flight.FeederFixTime,
            AssignedRunwayIdentifier = flight.AssignedRunwayIdentifier,
            NumberToLandOnRunway = sequence.NumberForRunway(flight),
            InitialLandingEstimate = flight.InitialLandingEstimate,
            LandingEstimate = flight.LandingEstimate,
            LandingTime = flight.LandingTime,
            InitialDelay = flight.TotalDelay,
            RemainingDelay = flight.RemainingDelay,
            FlowControls = flight.FlowControls,
            IsDummy = flight.IsDummy,
            ActivatedTime = flight.ActivatedTime,
            HighPriority = flight.HighPriority,
            NoDelay = flight.NoDelay,
            EstimatedTimeEnroute = flight.EstimatedTimeEnroute,
            ManualFeederFixEstimate = flight.ManualFeederFixEstimate,
            ActualFeederFixTime = flight.ActualFeederFixTime,
            RunwayManuallyAssigned = flight.RunwayManuallyAssigned,
            AssignedArrivalIdentifier = flight.AssignedArrivalIdentifier,
            ManualLandingTime = flight.ManualLandingTime,
            LastSeen = flight.LastSeen,
            Fixes = flight.Fixes,
            Position = flight.Position,
        };
    }
}
