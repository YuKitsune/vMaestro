using System.Text.Json.Serialization;
using Maestro.Core.Model;

namespace Maestro.Core.Configuration;

public class AirportConfigurationV2
{
    /// <summary>
    ///     The ICAO code of the airport.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The feeder fixes available at this airport.
    /// </summary>
    public required string[] FeederFixes { get; init; }

    /// <summary>
    ///     The runways available at this airport.
    /// </summary>
    public required string[] Runways { get; init; }

    // Defaults

    /// <summary>
    ///     The default aircraft type to use when the aircraft type cannot be determined for a flight,
    ///     or when inserting a flight manually, and no aircraft type is specified.
    /// </summary>
    public string DefaultAircraftType { get; init; } = "B738";

    /// <summary>
    ///     The default <see cref="State"/> of pending flights on insertion into the sequence.
    /// </summary>
    public State DefaultPendingFlightState { get; init; } = State.Stable;

    /// <summary>
    ///     The default <see cref="State"/> of departures on insertion into the sequence.
    /// </summary>
    public State DefaultDepartureFlightState { get; init; } = State.Unstable;

    /// <summary>
    ///     The default <see cref="State"/> of dummy flights on insertion into the sequence.
    /// </summary>
    public State DefaultDummyFlightState { get; init; } = State.Frozen;

    /// <summary>
    ///     The <see cref="State"/> to move flights into once manual interaction has been performed by the controller.
    /// </summary>
    public State ManualInteractionState { get; init; } = State.Stable;

    // State transition times

    /// <summary>
    ///     The maximum amount of time a flight must be away from landing before it is tracked by Maestro.
    /// </summary>
    public int FlightCreationThresholdMinutes { get; init; } = 120;

    /// <summary>
    ///     The minimum amount of time a flight must be considered <see cref="State.Unstable"/> before it may progress to <see cref="State.Stable"/>.
    /// </summary>
    public int MinimumUnstableMinutes { get; init; } = 5;

    /// <summary>
    ///     The amount of time before the STA_FF that a flight will transition to the <see cref="State.Stable"/> state.
    /// </summary>
    public int StabilityThresholdMinutes { get; init; } = 25;

    /// <summary>
    ///     The amount of time before the STA that a flight will transition to the <see cref="State.Frozen"/> state.
    /// </summary>
    public int FrozenThresholdMinutes { get; init; } = 15;

    // Retention

    /// <summary>
    ///     The maximum number of <see cref="State.Landed"/> flights to retain after landing.
    /// </summary>
    public int MaxLandedFlights { get; init; } = 5;

    /// <summary>
    ///     The maximum amount of time before <see cref="State.Landed"/> flights are removed.
    /// </summary>
    public int LandedFlightTimeoutMinutes { get; init; } = 10;

    /// <summary>
    ///     The maximum amount of time before lost flights are removed.
    /// </summary>
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
    /// <summary>
    ///     The identifier for this runway mode.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The minimum amount of separation to apply to flights landing on different runways in this runway mode.
    /// </summary>
    public int DependencyRateSeconds { get; init; } = 0;

    /// <summary>
    ///     The minimum amount of separation to apply to flights landing on a runway not defined in this runway mode.
    /// </summary>
    public int OffModeSeparationSeconds { get; init; } = 0;

    public required RunwayConfigurationV2[] Runways { get; init; }
}

public class RunwayConfigurationV2
{
    /// <summary>
    ///     The identifier of the runway.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The Approach Type to use for this runway, if any.
    /// </summary>
    public string ApproachType { get; init; } = string.Empty;

    /// <summary>
    ///     The minimum amount of separation to apply between flights landing on this runway.
    /// </summary>
    public required int LandingRateSeconds { get; init; }

    /// <summary>
    ///     The feeder fixes which must be tracked via for flights to be assigned to this runway.
    /// </summary>
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
    public double TrackMiles { get; init; } = 0.0; // Reserved for future use

    /// <summary>
    ///     The amount of time an aircraft matching <see cref="Aircraft"/> would take to fly from the
    ///     <see cref="FeederFix"/>, via the <see cref="ApproachType"/> to the <see cref="RunwayIdentifier"/>.
    /// </summary>
    public required int TimeToGoMinutes { get; init; }
    public int PressureMinutes { get; init; } = 0; // Reserved for future use
    public int MaxPressureMinutes { get; init; } = 0; // Reserved for future use
}

public class ViewConfigurationV2
{
    /// <summary>
    ///     The Identifier for this view.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The Identifier of the label layout to use in this view.
    /// </summary>
    public required string LabelLayout { get; init; }

    /// <summary>
    ///     The time which flights will be positioned on the ladder by.
    ///     If set to <see cref="LadderReference.LandingTime"/>, flights will be positioned on the lader based on their STA.
    ///     If set to <see cref="LadderReference.FeederFixTime"/>, flights will be positioned on the lader based on their STA_FF.
    /// </summary>
    public required LadderReference Reference { get; init; }

    /// <summary>
    ///     The window of time to be displayed in the sequence display area.
    /// </summary>
    public required int TimeWindowMinutes { get; init; }

    /// <summary>
    ///     The direction the ladder should scroll.
    /// </summary>
    public required LadderDirection Direction { get; init; }

    public required LadderConfigurationV2[] Ladders { get; init; }
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
    /// <summary>
    ///     The feeder fixes to filter this ladder to.
    /// </summary>
    public string[] FeederFixes { get; init; } = [];

    /// <summary>
    ///     The runways to filter this ladder to.
    /// </summary>
    public string[] Runways { get; init; } = [];
}

public class DepartureConfigurationV2
{
    // Lookup Parameters
    public required string Identifier { get; init; }
    // public required string FeederFix { get; init; } // Reserved for future use
    public required IAircraftDescriptor[] Aircraft { get; init; } // TODO: Match any

    // Lookup Values
    public required double Distance { get; init; } // Reserved for future use

    /// <summary>
    ///     The time it would take for an aircraft matching <see cref="Aircraft"/> to fly from <see cref="Identifier"/>
    ///     to the sequenced airport.
    /// </summary>
    public required int EstimatedFlightTimeMinutes { get; init; }
}

// Colour mappings - per-airport since runways/feeder fixes/approach types vary by airport
public class AirportColourConfigurationV2
{
    /// <summary>
    ///     The colors to apply to specific runways.
    /// </summary>
    public Dictionary<string, string> Runways { get; init; } = new();

    /// <summary>
    ///     The colors to apply to specific approach types.
    /// </summary>
    public Dictionary<string, string> ApproachTypes { get; init; } = new();

    /// <summary>
    ///     The colors to apply to specific feeder fixes.
    /// </summary>
    public Dictionary<string, string> FeederFixes { get; init; } = new();
}

// Shared colour mappings - same across all airports
public class ColourConfigurationV2
{
    /// <summary>
    ///     The colors to apply to specific states.
    /// </summary>
    public Dictionary<State, string> States { get; init; } = new();

    /// <summary>
    ///     The colors to apply to specific control actions.
    /// </summary>
    public Dictionary<ControlAction, string> ControlActions { get; init; } = new();

    /// <summary>
    ///     The color to apply to flights scheduled to land in a deferred runway mode.
    /// </summary>
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
    /// <summary>
    ///     The identifier of this label layout.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The label items, from innermost to outermost.
    /// </summary>
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
