using MessagePack;

namespace Maestro.Contracts.Flights;

/// <summary>
/// Represents a flight in the pending list.
/// </summary>
[MessagePackObject]
public class PendingFlightDto
{
    [Key(0)]
    public required string Callsign { get; init; }

    [Key(1)]
    public required string? AircraftType { get; init; }

    [Key(2)]
    public required string? OriginIdentifier { get; init; }

    [Key(3)]
    public required string DestinationIdentifier { get; init; }

    [Key(4)]
    public required bool IsFromDepartureAirport { get; init; }

    [Key(5)]
    public required bool IsHighPriority { get; init; }
}
