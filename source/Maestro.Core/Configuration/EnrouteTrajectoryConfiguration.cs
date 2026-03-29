namespace Maestro.Core.Configuration;

public class EnrouteTrajectoryConfiguration
{
    /// <summary>
    ///     The name of the waypoint along the flight plan route where the enroute phase begins.
    /// </summary>
    public required string EntryPoint { get; init; }

    /// <summary>
    ///     The feeder fix this trajectory applies to.
    /// </summary>
    public required string FeederFix { get; init; }

    /// <summary>
    ///     Maximum delay absorbable in the en-route area via linear techniques (speed reduction or path stretching).
    /// </summary>
    public required TimeSpan MaxEnrouteLinearDelay { get; init; }

    /// <summary>
    ///     Time savings available by flying a direct route (short-cut) through the en-route area.
    /// </summary>
    public TimeSpan ShortCutTimeToGain { get; init; } = TimeSpan.Zero;
}
