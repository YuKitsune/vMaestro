namespace Maestro.Core.Configuration;

public class TerminalTrajectoryConfiguration
{
    // Lookup parameters
    public required string FeederFix { get; init; }
    public string TransitionFix { get; init; } = string.Empty;
    public string ApproachType { get; init; } = string.Empty;
    public required string RunwayIdentifier { get; init; }

    // Route geometry — segments ordered from feeder fix to runway threshold.
    // Track and distance are pre-computed offline (e.g. via the Maestro.Tools CLI).
    public required TrajectorySegmentConfiguration[] Segments { get; init; }

    // Pressure trajectory: alternative path ATC may use to absorb delay.
    // Diverges after the specified segment.
    // P (Pressure window) = ETI from feeder fix through After segment + ETI along alternative segments.
    public TrajectoryBranch? Pressure { get; init; }

    // Max pressure trajectory: extended alternative path for maximum delay absorption.
    // Diverges after the specified segment.
    // Pmax (Maximum Pressure window) = ETI from feeder fix through After segment + ETI along alternative segments.
    public TrajectoryBranch? MaxPressure { get; init; }

    /// <summary>
    ///     The delay, in seconds, that may be allocated to a flight for linear absorption (vectors or speed
    ///     control) within the TMA. Used in lieu of a <see cref="Pressure"/> trajectory. When neither is
    ///     given, the airport's <see cref="AirportConfiguration.DefaultPressureSeconds"/> applies.
    /// </summary>
    public int? PressureSeconds { get; init; }

    /// <summary>
    ///     The absolute maximum delay, in seconds, that may be allocated to a flight for linear absorption
    ///     (vectors or speed control) within the TMA. Delay beyond this value is allocated to the enroute
    ///     phase. Used in lieu of a <see cref="MaxPressure"/> trajectory. When neither is given, the
    ///     airport's <see cref="AirportConfiguration.DefaultMaxPressureSeconds"/> applies.
    /// </summary>
    public int? MaxPressureSeconds { get; init; }
}
