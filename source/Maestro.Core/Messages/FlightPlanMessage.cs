namespace Maestro.Core.Messages;

public class FlightPlanMessage
{
    public required string Callsign { get; init; }
    public required string DepartureIdentifier { get; init; }
    public required string DestinationIdentifier { get; init; }
    public required string AircraftType { get; init; }
    public required DateTimeOffset EstimatedDepartureTime { get; init; }
    public required TimeSpan EstimatedTimeEnroute { get; init; }
}
