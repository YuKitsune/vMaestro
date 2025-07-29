namespace Maestro.Core.Configuration;

public class RunwayConfiguration
{
    public required string Identifier { get; init; }

    // TODO: Time span please...
    public required int LandingRateSeconds { get; init; }
}
