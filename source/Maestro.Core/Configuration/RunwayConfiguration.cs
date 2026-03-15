namespace Maestro.Core.Configuration;

public class RunwayConfiguration
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
