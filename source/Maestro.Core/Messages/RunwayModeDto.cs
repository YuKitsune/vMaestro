namespace Maestro.Core.Messages;

/// <summary>
/// Represents a runway mode configuration.
/// </summary>
/// <param name="Identifier">The identifier for this runway mode (e.g., "SODPROPS").</param>
/// <param name="Runways">The runways available in this mode.</param>
/// <param name="DependencyRateSeconds">The minimum amount of separation to apply between flights landing on different runways in this mode, in seconds.</param>
/// <param name="OffModeSeparationSeconds">The minimum amount of separation to apply to flights landing on a runway not defined in this mode, in seconds.</param>
public record RunwayModeDto(
    string Identifier,
    RunwayDto[] Runways,
    int DependencyRateSeconds,
    int OffModeSeparationSeconds);

/// <summary>
/// Represents a runway configuration within a runway mode.
/// </summary>
/// <param name="Identifier">The identifier of the runway (e.g., "34L").</param>
/// <param name="ApproachType">The approach type to use for this runway, if any.</param>
/// <param name="AcceptanceRateSeconds">The minimum amount of separation to apply between flights landing on this runway, in seconds.</param>
/// <param name="FeederFixes">The feeder fixes which must be tracked via for flights to be assigned to this runway.</param>
public record RunwayDto(
    string Identifier,
    string ApproachType,
    int AcceptanceRateSeconds,
    string[] FeederFixes);
