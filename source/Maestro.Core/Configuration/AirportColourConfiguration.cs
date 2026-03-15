namespace Maestro.Core.Configuration;

/// <summary>
///     Per-airport color configuration, where colors for specific runways, feeder fixes, and approach types can be specified.
/// </summary>
public class AirportColourConfiguration
{
    /// <summary>
    ///     The colors to apply to specific runways.
    /// </summary>
    public Dictionary<string, Color> Runways { get; init; } = new();

    /// <summary>
    ///     The colors to apply to specific approach types.
    /// </summary>
    public Dictionary<string, Color> ApproachTypes { get; init; } = new();

    /// <summary>
    ///     The colors to apply to specific feeder fixes.
    /// </summary>
    public Dictionary<string, Color> FeederFixes { get; init; } = new();
}
