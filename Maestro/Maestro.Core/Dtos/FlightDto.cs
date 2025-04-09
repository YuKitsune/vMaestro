using Maestro.Core.Model;

namespace Maestro.Core.Dtos;

public record FlightDto(
    string Callsign,
    string AircraftType,
    WakeCategory WakeCategory,
    string Origin,
    string Destination,
    State State,
    string? FeederFix,
    string? AssignedRunway,
    string? AssignedStar,
    DateTimeOffset? InitialFeederFixTime,
    DateTimeOffset? EstimatedFeederFixTime,
    DateTimeOffset? ScheduledFeederFixTime,
    DateTimeOffset InitialLandingTime,
    DateTimeOffset EstimatedLandingTime,
    DateTimeOffset ScheduledLandingTime,
    TimeSpan InitialDelay,
    TimeSpan CurrentDelay);
