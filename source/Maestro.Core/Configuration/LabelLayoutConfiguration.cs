namespace Maestro.Core.Configuration;

/// <summary>
///     Reusable label layout.
/// </summary>
public class LabelLayoutConfiguration
{
    /// <summary>
    ///     The identifier of this label layout.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    ///     The label items, from innermost to outermost.
    /// </summary>
    public required LabelItemConfiguration[] Items { get; init; }
}

public abstract class LabelItemConfiguration
{
    public abstract LabelItemType Type { get; }
    public LabelItemColourSource[] ColourSources { get; init; } = [];
    public required int Width { get; init; }
    public int Padding { get; init; } = 0;
}

public class CallsignItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.Callsign;
}

public class AircraftTypeItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.AircraftType;
}

public class AircraftWakeCategoryItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.AircraftWakeCategory;
}

public class RunwayItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.Runway;
}

public class ApproachTypeItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.ApproachType;
}

public class LandingTimeItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.LandingTime;
}

public class FeederFixTimeItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.FeederFixTime;
}

public class RequiredDelayItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.RequiredDelay;
}

public class RemainingDelayItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.RemainingDelay;
}

public class ManualDelayItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.ManualDelay;
    public required string ZeroDelaySymbol { get; init; }
    public required string ManualDelaySymbol { get; init; }
}

public class ProfileSpeedItemConfiguration : LabelItemConfiguration
{
    public override LabelItemType Type => LabelItemType.ProfileSpeed;
    public required string Symbol { get; init; }
}

public class CouplingStatusItemConfiguration : LabelItemConfiguration
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
