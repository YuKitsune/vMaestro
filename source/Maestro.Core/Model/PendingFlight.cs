namespace Maestro.Core.Model;

/// <summary>
/// A lightweight record representing a flight in the pending list.
/// Full flight data is maintained separately in the flight data store.
/// </summary>
public record PendingFlight(
    string Callsign,
    bool IsFromDepartureAirport,
    bool IsHighPriority);
