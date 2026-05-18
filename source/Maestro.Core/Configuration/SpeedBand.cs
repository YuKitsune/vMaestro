namespace Maestro.Core.Configuration;

public class SpeedBand
{
    /// <summary>
    ///     This band's speed applies when distance-to-go is strictly greater than this value (nautical miles).
    ///     Set to 0 to define the floor speed covering the final approach to the runway.
    /// </summary>
    public required double ThresholdNM { get; init; }

    /// <summary>
    ///     True airspeed in knots for this distance band.
    /// </summary>
    public required int SpeedKnots { get; init; }
}
