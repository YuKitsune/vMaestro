namespace Maestro.Contracts.Runway;

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
