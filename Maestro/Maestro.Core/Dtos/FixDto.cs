namespace Maestro.Core.Dtos;

public record FixDto(
    string Identifier,
    Coordinate Position,
    DateTimeOffset Estimate);
