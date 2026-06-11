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
            RunwayAssignment = flight.RunwayAssignment.ToDto(),
            NumberToLandOnRunway = sequence.NumberForRunway(flight),
            InitialLandingEstimate = flight.InitialLandingEstimate,
            LandingEstimate = flight.LandingEstimate,
            TargetLandingTime = flight.TargetLandingTime,
            LandingTime = flight.LandingTime,
            HighSpeed = flight.HighSpeed,
            ActivatedTime = flight.ActivatedTime,
            HighPriority = flight.HighPriority,
            MaximumDelay = flight.MaximumDelay,
            ManualFeederFixEstimate = flight.ManualFeederFixEstimate,
            ApproachType = flight.ApproachType,
            Position = flight.Position,
            IsManuallyInserted = flight.IsManuallyInserted,
            TerminalNormalTimeToGo = flight.TerminalTrajectory.NormalTimeToGo,
            TerminalPressureTimeToGo = flight.TerminalTrajectory.PressureTimeToGo,
            TerminalMaxPressureTimeToGo = flight.TerminalTrajectory.MaxPressureTimeToGo,
            EnrouteShortcutTimeToGain = flight.EnrouteTrajectory.ShortcutTimeToGain,
            EnrouteMaxLinearDelay = flight.EnrouteTrajectory.MaxLinearEnrouteDelay,
            RequiredControlAction = flight.RequiredControlAction,
            RemainingControlAction = flight.RemainingControlAction,
            RequiredEnrouteDelay = flight.RequiredEnrouteDelay,
            RequiredTerminalDelay = flight.RequiredTerminalDelay,
            RemainingEnrouteDelay = flight.RemainingEnrouteDelay,
            RemainingTerminalDelay = flight.RemainingTerminalDelay
        };
    }

    public static AircraftPerformanceData GetPerformanceData(this Flight flight)
    {
        return new AircraftPerformanceData(flight.AircraftType, flight.AircraftCategory, flight.WakeCategory);
    }

    public static IRunwayAssignmentDto ToDto(this IRunwayAssignment runwayAssignment)
    {
        return runwayAssignment switch
        {
            ManualRunwayAssignment manual => new ManualRunwayAssignmentDto(manual.RunwayIdentifier),
            AutomaticRunwayAssignment automatic => new AutomaticRunwayAssignmentDto(automatic.RunwayIdentifier),
            _ => throw new ArgumentOutOfRangeException(nameof(runwayAssignment), runwayAssignment, "Unknown runway assignment type")
        };
    }

    public static IRunwayAssignment ToModel(this IRunwayAssignmentDto runwayAssignment)
    {
        return runwayAssignment switch
        {
            ManualRunwayAssignmentDto manual => new ManualRunwayAssignment(manual.RunwayIdentifier),
            AutomaticRunwayAssignmentDto automatic => new AutomaticRunwayAssignment(automatic.RunwayIdentifier),
            _ => throw new ArgumentOutOfRangeException(nameof(runwayAssignment), runwayAssignment, "Unknown runway assignment type")
        };
    }
}
