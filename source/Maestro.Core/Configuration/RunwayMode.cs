namespace Maestro.Core.Configuration;

public class RunwayMode
{
    public required string Identifier { get; init; }
    public required RunwayConfiguration[] Runways { get; init; }
}
