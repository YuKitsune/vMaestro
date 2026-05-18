namespace Maestro.Core.Configuration;

public class AircraftSpeedProfile
{
    /// <summary>
    ///     Descriptors identifying which aircraft this profile applies to.
    ///     Supports type codes (e.g. "DH8D"), category keywords ("Jet", "Prop"), wake categories ("Heavy"), or "All".
    ///     When matching, an exact type-code match scores higher than a category match.
    /// </summary>
    public required IAircraftDescriptor[] AircraftTypes { get; init; }

    /// <summary>
    ///     Speed schedule ordered descending by <see cref="SpeedBand.ThresholdNM"/>.
    ///     A band applies when distance-to-go is strictly greater than <see cref="SpeedBand.ThresholdNM"/>.
    ///     The last band (ThresholdNM = 0) acts as the floor and covers the final approach.
    /// </summary>
    public required SpeedBand[] Speeds { get; init; }
}
