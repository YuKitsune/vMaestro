using MessagePack;

namespace Maestro.Contracts.Runway;

/// <summary>
/// Represents a runway configuration within a runway mode.
/// </summary>
/// <param name="Identifier">The identifier of the runway (e.g., "34L").</param>
/// <param name="ApproachType">The approach type to use for this runway, if any.</param>
/// <param name="AcceptanceRateSeconds">The minimum amount of separation to apply between flights landing on this runway, in seconds.</param>
/// <param name="FeederFixes">The feeder fixes which must be tracked via for flights to be assigned to this runway.</param>
[MessagePackObject]
public record RunwayDto(
    [property: Key(0)] string Identifier,
    [property: Key(1)] string ApproachType,
    [property: Key(2)] int AcceptanceRateSeconds,
    [property: Key(3)] string[] FeederFixes);
