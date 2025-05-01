namespace Maestro.Core.Configuration;

public class RunwayModeConfiguration
{
    public required string Identifier { get; init; }
    public required RunwayConfiguration[] Runways { get; init; }
}
