using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class AirportConfigurationV2
{
    public required string Identifier { get; init; }
    public required string[] FeederFixes { get; init; }
    public required string[] Runways { get; init; }

    // Defaults
    public string DefaultAircraftType { get; init; } = "B738";
    public State DefaultPendingFlightState { get; init; } = State.Stable;
    public State DefaultDepartureFlightState { get; init; } = State.Unstable;
    public State DefaultDummyFlightState { get; init; } = State.Frozen;
    public int DefaultOffModeSeparationSeconds { get; init; } = 300;

    // State transition times
    public int MinimumUnstableMinutes { get; init; } = 5;
    public int StabilityThresholdMinutes { get; init; } = 25;
    public int FrozenThresholdMinutes { get; init; } = 15;

    // Retention
    public int MaxLandedFlights { get; init; } = 5;
    public int LandedFlightTimeoutMinutes { get; init; } = 10;
    public int LostFlightTimeoutMinutes { get; init; } = 10;

    public required RunwayModeConfigurationV2[] RunwayModes { get; init; }

    // Look-up tables
    public required TrajectoryConfigurationV2[] Trajectories { get; init; }
    public required DepartureConfigurationV2[] DepartureAirports { get; init; }

    // TODO: Close airports
    // TODO: Average taxi times and terminal assignments
}

public class RunwayModeConfigurationV2
{
    public required string Identifier { get; init; }
    public int DependencyRateSeconds { get; init; } = 0;
    public int OffModeSeparationSeconds { get; init; } = 0;
    public required RunwayConfigurationV2[] Runways { get; init; }
}

public class RunwayConfigurationV2
{
    public required string Identifier { get; init; }
    public string ApproachType { get; init; } = string.Empty;
    public required int LandingRateSeconds { get; init; }
    public string[] FeederFixes { get; init; } = [];
}

public class TrajectoryConfigurationV2
{
    // Lookup Parameters
    public required string FeederFix { get; init; }
    public required IAircraftDescriptor[] Aircraft { get; init; } // TODO: Match any
    public string ApproachType { get; init; } = string.Empty;
    public string ApproachFix { get; init; } = string.Empty;
    public required string RunwayIdentifier { get; init; }

    // Lookup Values
    public required double TrackMiles { get; init; }
    public required int TimeToGoMinutes { get; init; }
    public int PressureMinutes { get; init; }
    public int MaxPressureMinutes { get; init; }
}

public class ViewConfigurationV2
{
    public required string Identifier { get; init; }
    public required int TimeHorizonMinutes { get; init; }
    // TODO: Naming, Timeline vs Ladder?
    public required LadderReference LadderReference { get; init; }
    public required ILadderConfiguration[] Ladders { get; init; }
    // TODO: Labels
    // TODO: What else?
}

public enum LadderReference
{
    LandingTime,
    FeederFixTime
}

public interface ILadderConfiguration;

public record FeederFixLadderConfiguration(string[] FeederFixes) : ILadderConfiguration;

public record RunwayLadderConfiguration(string[] RunwayIdentifiers) : ILadderConfiguration;

public class DepartureConfigurationV2
{
    // Lookup Parameters
    public required string Identifier { get; init; }
    // public required string FeederFix { get; init; }
    public required IAircraftDescriptor[] Aircraft { get; init; } // TODO: Match any

    // Lookup Values
    public required double Distance { get; init; }
    public required int EstimatedFlightTimeMinutes { get; init; }
}
