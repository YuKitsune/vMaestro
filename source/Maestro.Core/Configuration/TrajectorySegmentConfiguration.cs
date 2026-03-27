namespace Maestro.Core.Configuration;

public class TrajectorySegmentConfiguration
{
    /// <summary>
    ///     The fix identifier for this segment. Informational only — never resolved at runtime.
    /// </summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    ///     The true track (bearing) in degrees for this segment.
    /// </summary>
    public required double Track { get; init; }

    /// <summary>
    ///     The distance of this segment in nautical miles.
    /// </summary>
    public required double DistanceNM { get; init; }

    /// <summary>
    ///     When true, the ETI for this segment contributes to the Pressure window (P).
    /// </summary>
    public bool Pressure { get; init; } = false;

    /// <summary>
    ///     When true, the ETI for this segment contributes to the Maximum Pressure window (Pmax).
    /// </summary>
    public bool MaxPressure { get; init; } = false;
}
