namespace Maestro.Core.Configuration;

public class TrajectoryConfiguration
{
    // Lookup Parameters
    public required string FeederFix { get; init; }
    public required IAircraftDescriptor[] Aircraft { get; init; }
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
