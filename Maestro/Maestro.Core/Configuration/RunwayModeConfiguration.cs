namespace Maestro.Core.Configuration;

public class RunwayModeConfiguration
{
    public required string Identifier { get; init; }
    public required int StaggerRateSeconds { get; init; }
    public required RunwayConfiguration[] Runways { get; init; }
    
    // TODO: Runway assignment rules 
}
