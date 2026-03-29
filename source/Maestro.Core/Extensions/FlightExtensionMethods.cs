using Maestro.Contracts.Flights;
using Maestro.Core.Integration;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class FlightExtensionMethods
{
    public static FlightDto ToDto(this Flight flight, Sequence sequence)
    {
        return new FlightDto
        {
            Callsign = flight.Callsign,
            AircraftType = flight.AircraftType,
            AircraftCategory = flight.AircraftCategory,
            WakeCategory = flight.WakeCategory,
            OriginIdentifier = flight.OriginIdentifier,
            DestinationIdentifier = flight.DestinationIdentifier,
            IsFromDepartureAirport = flight.IsFromDepartureAirport,
            EstimatedDepartureTime = flight.EstimatedDepartureTime,
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
            TargetLandingTime = flight.TargetLandingTime,
            LandingTime = flight.LandingTime,
            FlowControls = flight.FlowControls,
            ActivatedTime = flight.ActivatedTime,
            HighPriority = flight.HighPriority,
            MaximumDelay = flight.MaximumDelay,
            ManualFeederFixEstimate = flight.ManualFeederFixEstimate,
            ApproachType = flight.ApproachType,
            LastSeen = flight.LastSeen,
            Position = flight.Position,
            IsManuallyInserted = flight.IsManuallyInserted,
            NormalTimeToGo = flight.TerminalTrajectory.NormalTimeToGo,
            PressureTimeToGo = flight.TerminalTrajectory.PressureTimeToGo,
            MaxPressureTimeToGo = flight.TerminalTrajectory.MaxPressureTimeToGo,
            EnrouteShortCutTimeToGain = flight.EnrouteTrajectory.ShortCutTimeToGain,
            MaxEnrouteLinearDelay = flight.EnrouteTrajectory.MaxLinearEnrouteDelay,
            RequiredControlAction = flight.RequiredControlAction,
            RemainingControlAction = flight.RemainingControlAction,
            RequiredEnrouteDelay = flight.RequiredEnrouteDelay,
            RemainingEnrouteDelay = flight.RemainingEnrouteDelay
        };
    }

    public static AircraftPerformanceData GetPerformanceData(this Flight flight)
    {
        return new AircraftPerformanceData(flight.AircraftType, flight.AircraftCategory, flight.WakeCategory);
    }
}
