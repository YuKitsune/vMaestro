using MessagePack;

namespace Maestro.Contracts.Runway;

/// <summary>
/// Represents a runway mode configuration.
/// </summary>
/// <param name="Identifier">The identifier for this runway mode (e.g., "SODPROPS").</param>
/// <param name="Runways">The runways available in this mode.</param>
/// <param name="DependencyRateSeconds">The minimum amount of separation to apply between flights landing on different runways in this mode, in seconds.</param>
/// <param name="OffModeSeparationSeconds">The minimum amount of separation to apply to flights landing on a runway not defined in this mode, in seconds.</param>
[MessagePackObject]
public record RunwayModeDto(
    [property: Key(0)] string Identifier,
    [property: Key(1)] RunwayDto[] Runways,
    [property: Key(2)] int DependencyRateSeconds,
    [property: Key(3)] int OffModeSeparationSeconds);
