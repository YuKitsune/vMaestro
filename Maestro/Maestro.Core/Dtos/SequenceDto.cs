namespace Maestro.Core.Dtos;

public record SequenceDto(
    string AirportIdentifier,
    FlightDto[] Flights);
