namespace TFMS.Core.DTOs;

public record FlightDataRecord(
    string Callsign,
    string OriginIcaoCode,
    string DestinationIcaoCode,
    string? AssignedRunway,
    string? AssignedStar,
    Fix[] Estimates);

public record Fix(string Identifier, DateTimeOffset Estimate);
