using Maestro.Core.Model;

namespace Maestro.Core.Dtos;

public record FlightDto(
    string Callsign,
    string AircraftType,
    string Origin,
    string Destination,
    StateDto State,
    string? FeederFix,
    string? AssignedRunway,
    string? AssignedStar,
    DateTimeOffset? InitialFeederFixTime,
    DateTimeOffset? EstimatedFeederFixTime,
    DateTimeOffset? ScheduledFeederFixTime,
    DateTimeOffset InitialLandingTime,
    DateTimeOffset EstimatedLandingTime,
    DateTimeOffset ScheduledLandingTime);
