namespace TFMS.Core.Dtos;

public record FlightDTO(
    string Callsign,
    string AircraftType,
    string OriginIcao,
    string DestinationIcao,
    StateDTO State,
    string FeederFix,
    string? AssignedRunway,
    string? AssignedStar,
    DateTimeOffset InitialFeederFixTime,
    DateTimeOffset EstimatedFeederFixTime,
    DateTimeOffset ScheduledFeederFixTime,
    DateTimeOffset InitialLandingTime,
    DateTimeOffset EstimatedLandingTime,
    DateTimeOffset ScheduledLandingTime);
