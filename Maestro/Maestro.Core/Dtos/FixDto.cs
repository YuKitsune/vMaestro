using Maestro.Core.Model;

namespace Maestro.Core.Dtos;

public record FixDto(
    string Identifier,
    DateTimeOffset Estimate);
