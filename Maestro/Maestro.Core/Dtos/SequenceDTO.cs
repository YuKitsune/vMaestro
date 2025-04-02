namespace Maestro.Core.Dtos;

public record SequenceDTO(
    string AirportIdentifier,
    FlightDTO[] Arrivals);
