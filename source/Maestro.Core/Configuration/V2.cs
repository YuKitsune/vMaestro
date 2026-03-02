using System.Text.Json.Serialization;
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

    // Everything beyond this point is purely for presentation, and not used within Maestro.Core

    // Presentation
    public AirportColourConfigurationV2? Colours { get; init; }
    public required ViewConfigurationV2[] Views { get; init; }

    // Coordination Messages
    public required string[] GlobalCoordinationMessages { get; init; }
    public required string[] FlightCoordinationMessages { get; init; }
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
    public Dictionary<ControlAction, string> ControlActions { get; init; } = new();
    public string DeferredRunwayMode { get; init; } = string.Empty;
}

public class LabelsConfigurationV2
{
    public required ColourConfigurationV2 Colours { get; init; }
    public required LabelLayoutConfigurationV2[] Layouts { get; init; }
}

// Reusable label layout - defined once, referenced by views
public class LabelLayoutConfigurationV2
{
    public required string Identifier { get; init; }
    public required LabelItemConfigurationV2[] Items { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(CallsignItemConfigurationV2), nameof(LabelItemType.Callsign))]
[JsonDerivedType(typeof(AircraftTypeItemConfigurationV2), nameof(LabelItemType.AircraftType))]
[JsonDerivedType(typeof(AircraftWakeCategoryItemConfigurationV2), nameof(LabelItemType.AircraftWakeCategory))]
[JsonDerivedType(typeof(RunwayItemConfigurationV2), nameof(LabelItemType.Runway))]
[JsonDerivedType(typeof(ApproachTypeItemConfigurationV2), nameof(LabelItemType.ApproachType))]
[JsonDerivedType(typeof(LandingTimeItemConfigurationV2), nameof(LabelItemType.LandingTime))]
[JsonDerivedType(typeof(FeederFixTimeItemConfigurationV2), nameof(LabelItemType.FeederFixTime))]
[JsonDerivedType(typeof(RequiredDelayItemConfigurationV2), nameof(LabelItemType.RequiredDelay))]
[JsonDerivedType(typeof(RemainingDelayItemConfigurationV2), nameof(LabelItemType.RemainingDelay))]
[JsonDerivedType(typeof(ManualDelayItemConfigurationV2), nameof(LabelItemType.ManualDelay))]
[JsonDerivedType(typeof(ProfileSpeedItemConfigurationV2), nameof(LabelItemType.ProfileSpeed))]
[JsonDerivedType(typeof(CouplingStatusItemConfigurationV2), nameof(LabelItemType.CouplingStatus))]
public abstract class LabelItemConfigurationV2
{
    public abstract LabelItemType Type { get; }
    public LabelItemColourSource[] ColourSources { get; init; } = [LabelItemColourSource.State];
    public required int Width { get; init; }
    public int Padding { get; init; } = 1;
}

public class CallsignItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.Callsign;
}

public class AircraftTypeItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.AircraftType;
}

public class AircraftWakeCategoryItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.AircraftWakeCategory;
}

public class RunwayItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.Runway;
}

public class ApproachTypeItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.ApproachType;
}

public class LandingTimeItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.LandingTime;
}

public class FeederFixTimeItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.FeederFixTime;
}

public class RequiredDelayItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.RequiredDelay;
    public required string ZeroDelaySymbol { get; init; }
}

public class RemainingDelayItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.RemainingDelay;
}

public class ManualDelayItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.ManualDelay;
    public required string ZeroDelaySymbol { get; init; }
    public required string ManualDelaySymbol { get; init; }
}

public class ProfileSpeedItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.ProfileSpeed;
    public required string Symbol { get; init; }
}

public class CouplingStatusItemConfigurationV2 : LabelItemConfigurationV2
{
    public override LabelItemType Type => LabelItemType.CouplingStatus;
    public required string UncoupledSymbol { get; init; }
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

public static class ColourDefaults
{
    public static ColourConfigurationV2 Default() => new()
    {
        States = new Dictionary<State, string>
        {
            { State.Unstable, "255,205,105" },
            { State.Stable, "0, 0, 96" },
            { State.SuperStable, "255, 255, 255" },
            { State.Frozen, "96, 0, 0" },
            { State.Landed, "0, 235, 235" }
        },
        ControlActions = new Dictionary<ControlAction, string>
        {
            { ControlAction.Expedite, "0, 105, 0" },
            { ControlAction.NoDelay, "0, 0, 96" },
            { ControlAction.Resume, "0, 0, 96" },
            { ControlAction.SpeedReduction, "0, 235, 235" },
            { ControlAction.PathStretching, "255, 255, 255" },
            { ControlAction.Holding, "235, 235, 0" }
        },
        DeferredRunwayMode = "255,255,255"
    };
}

public static class LabelItemDefaults
{
    public static LabelItemConfigurationV2[] DefaultEnrouteLabelItem()
    {
        return
        [
            new FeederFixTimeItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 2
            },
            new RunwayItemConfigurationV2
            {
                ColourSources =
                    [LabelItemColourSource.RunwayMode, LabelItemColourSource.Runway, LabelItemColourSource.State],
                Width = 3,
                Padding = 1
            },
            new CallsignItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 10,
                Padding = 1
            },
            new ApproachTypeItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.ApproachType, LabelItemColourSource.State],
                Width = 1,
                Padding = 1
            },
            new ManualDelayItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0,
                ZeroDelaySymbol = "#",
                ManualDelaySymbol = "%"
            },
            new ProfileSpeedItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0,
                Symbol = "+"
            },
            new CouplingStatusItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0,
                UncoupledSymbol = "*"
            },
            new RequiredDelayItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.ControlAction],
                Width = 2,
                Padding = 1,
                ZeroDelaySymbol = "#"
            },
            new RemainingDelayItemConfigurationV2
            {
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
            new LandingTimeItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 2
            },
            new RunwayItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.RunwayMode, LabelItemColourSource.Runway, LabelItemColourSource.State],
                Width = 3,
                Padding = 1
            },
            new CallsignItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 10,
                Padding = 1
            },
            new ApproachTypeItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.ApproachType, LabelItemColourSource.State],
                Width = 1,
                Padding = 1
            },
            new ManualDelayItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0,
                ZeroDelaySymbol = "#",
                ManualDelaySymbol = "%"
            },
            new ProfileSpeedItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0,
                Symbol = "+"
            },
            new CouplingStatusItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.State],
                Width = 1,
                Padding = 0,
                UncoupledSymbol = "*"
            },
            new RemainingDelayItemConfigurationV2
            {
                ColourSources = [LabelItemColourSource.ControlAction],
                Width = 2,
                Padding = 0
            }
        ];
    }
}
