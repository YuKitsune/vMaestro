namespace Maestro.Core.Configuration;

public class AirportConfiguration
{
    public required string Identifier { get; init; }
    public required double MinimumRadarEstimateRange { get; init; } = 150;
    public required string[] FeederFixes { get; init; }
    public required RunwayConfiguration[] Runways { get; init; }
    public required RunwayMode[] RunwayModes { get; init; }
    public required ArrivalConfiguration[] Arrivals { get; init; }
    public required ViewConfiguration[] Views { get; init; }
    public required RunwayAssignmentRule[] RunwayAssignmentRules { get; init; }
    public required string[] DepartureAirports { get; init; } = [];
}
