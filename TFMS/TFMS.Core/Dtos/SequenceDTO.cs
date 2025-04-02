namespace TFMS.Core.Dtos;

public record SequenceDTO(
    string AirportIdentifier,
    FlightDTO[] Arrivals);
