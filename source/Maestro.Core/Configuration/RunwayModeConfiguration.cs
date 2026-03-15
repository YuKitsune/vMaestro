namespace Maestro.Core.Configuration;

public class RunwayModeConfiguration
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

    public required RunwayConfiguration[] Runways { get; init; }
}
