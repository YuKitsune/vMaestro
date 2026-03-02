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
    public required int TimeWindowMinutes { get; init; }

    // TODO: Naming, Timeline vs Ladder?
    public required LadderDirection Direction { get; init; }
    public required ViewReference Reference { get; init; }
    public required LadderConfigurationV2[] Ladders { get; init; }
    public required LabelItemConfiguration[] LabelItems { get; init; }
}

public enum ViewReference
{
    LandingTime,
    FeederFixTime
}

public enum LadderDirection
{
    Down,
    Up
}

public class LadderConfigurationV2
{
    public string[] FeederFixes { get; init; } = [];
    public string[] Runways { get; init; } = [];
}

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

public class LabelItemConfiguration
{
    public required LabelItemType Type { get; init; }
    public required LabelItemColour Colour { get; init; }
}

public enum LabelItemType
{
    Callsign,
    AircraftType,
    AircraftWakeCategory,
    Runway,
    ApproachType,
    LandingTime,
    FeederFixTime,
    TotalDelay,
    RemainingDelay,
    // TODO: ProfileSpeed symbol
    // TODO: ManualDelay symbol
    // TODO: Coupling status symbol
}

// TODO: Find a better way to make colours dependent the value of certain properties
public enum LabelItemColourSource
{
    ApproachType,
    FeederFix,
    RunwayMode,
    Runway,
    Status
}

public class LabelItemColour
{
    public required LabelItemColourSource Source { get; init; }
    public required string Value { get; init; }
    public required string Colour { get; init; }
}
