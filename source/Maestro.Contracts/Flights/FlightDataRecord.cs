using Maestro.Contracts.Shared;
using MessagePack;

namespace Maestro.Contracts.Flights;

/// <summary>
/// Represents the latest flight data received from the air traffic management system.
/// Contains only data provided by the source system — no sequencing information.
/// </summary>
[MessagePackObject]
public record FlightDataRecord(
    [property: Key(0)] string Callsign,
    [property: Key(1)] string AircraftType,
    [property: Key(2)] AircraftCategory AircraftCategory,
    [property: Key(3)] WakeCategory WakeCategory,
    [property: Key(4)] string? Origin,
    [property: Key(5)] string Destination,
    [property: Key(6)] DateTimeOffset? EstimatedDepartureTime,
    [property: Key(7)] FlightPosition? Position,
    [property: Key(8)] FixEstimate[] Estimates,
    [property: Key(9)] DateTimeOffset LastSeen);
