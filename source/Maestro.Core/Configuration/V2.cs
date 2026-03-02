using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class AirportConfigurationV2
{
    public required string Identifier { get; init; }
    public required string[] FeederFixes { get; init; }
    public required string[] Runways { get; init; }

    public required AirportColourConfigurationV2 Colours { get; init; }

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

    public required LadderDirection Direction { get; init; }
    public required LadderReference Reference { get; init; }
    public required LadderConfigurationV2[] Ladders { get; init; }
    public required string LabelLayout { get; init; }
}

public enum LadderReference
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

// Colour mappings - per-airport since runways/feeder fixes/approach types vary by airport
public class AirportColourConfigurationV2
{
    public Dictionary<string, string> Runways { get; init; } = new();
    public Dictionary<string, string> ApproachTypes { get; init; } = new();
    public Dictionary<string, string> FeederFixes { get; init; } = new();
}

// Shared colour mappings - same across all airports
public class ColourConfigurationV2
{
    public Dictionary<State, string> States { get; init; } = new();
    public Dictionary<string, string> ControlActions { get; init; } = new();
    public string DeferredRunwayMode { get; init; } = string.Empty;
}

public class LabelLayoutsConfigurationV2
{
    public required ColourConfigurationV2 Colours { get; init; }
    public required SymbolConfigurationV2 Symbols { get; init; }
    public required LabelLayoutConfigurationV2[] Layouts { get; init; }
}

// Symbol configuration - shared across all airports
public class SymbolConfigurationV2
{
    public required string ZeroDelay { get; init; }
    public required string ManualDelay { get; init; }
    public required string ProfileSpeed { get; init; }
    public required string CouplingStatus { get; init; }
}

// Reusable label layout - defined once, referenced by views
public class LabelLayoutConfigurationV2
{
    public required string Identifier { get; init; }
    public required LabelItemConfigurationV2[] Items { get; init; }
}

public class LabelItemConfigurationV2
{
    public required LabelItemType Type { get; init; }
    public LabelItemColourSource[] ColourSources { get; init; } = [LabelItemColourSource.State];
    public required int Width { get; init; }
    public int Padding { get; init; } = 1;
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
    RequiredDelay,
    RemainingDelay,
    ManualDelay,
    ProfileSpeed,
    CouplingStatus,
}

public enum LabelItemColourSource
{
    Runway,
    ApproachType,
    FeederFix,
    State,
    RunwayMode,
    ControlAction,
}

public static class LabelItemDefaults
{
    public static LabelItemConfigurationV2[] DefaultEnrouteLabelItem()
    {
        return
        [
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.FeederFixTime,
                ColourSources = [LabelItemColourSource.State],
                Width = 2
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.Runway,
                ColourSources =
                    [LabelItemColourSource.RunwayMode, LabelItemColourSource.Runway, LabelItemColourSource.State],
                Width = 3,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.Callsign,
                ColourSources = [LabelItemColourSource.State],
                Width = 10,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.ApproachType,
                ColourSources = [LabelItemColourSource.ApproachType, LabelItemColourSource.State],
                Width = 1,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.ManualDelay,
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.ProfileSpeed,
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.CouplingStatus,
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.RequiredDelay,
                ColourSources = [LabelItemColourSource.ControlAction],
                Width = 2,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.RemainingDelay,
                ColourSources = [LabelItemColourSource.ControlAction],
                Width = 2,
                Padding = 0
            }
        ];
    }

    public static LabelItemConfigurationV2[] DefaultTmaLabelItem()
    {
        return
        [
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.LandingTime,
                ColourSources = [LabelItemColourSource.State],
                Width = 2
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.Runway,
                ColourSources = [LabelItemColourSource.RunwayMode, LabelItemColourSource.Runway, LabelItemColourSource.State],
                Width = 3,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.Callsign,
                ColourSources = [LabelItemColourSource.State],
                Width = 10,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.ApproachType,
                ColourSources = [LabelItemColourSource.ApproachType, LabelItemColourSource.State],
                Width = 1,
                Padding = 1
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.ManualDelay,
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.ProfileSpeed,
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.CouplingStatus,
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0
            },
            new LabelItemConfigurationV2
            {
                Type = LabelItemType.RemainingDelay,
                ColourSources = [LabelItemColourSource.ControlAction],
                Width = 2,
                Padding = 0
            }
        ];
    }
}
