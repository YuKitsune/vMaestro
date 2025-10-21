namespace Maestro.Core.Configuration;

public class AirportConfiguration
{
    public required string Identifier { get; init; }
    public required string[] FeederFixes { get; init; }
    public required Dictionary<string, string[]> PreferredRunways { get; init; } = new();
    public required RunwayConfiguration[] Runways { get; init; }
    public required RunwayModeConfiguration[] RunwayModes { get; init; }
    public required ArrivalConfiguration[] Arrivals { get; init; }
    public required ViewConfiguration[] Views { get; init; }
    public required string[] DepartureAirports { get; init; } = [];
}
