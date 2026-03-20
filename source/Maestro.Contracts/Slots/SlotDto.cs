namespace Maestro.Contracts.Slots;

/// <summary>
/// Represents a time slot during which certain runways are unavailable.
/// </summary>
/// <param name="Id">The unique identifier for this slot.</param>
/// <param name="StartTime">When the slot begins.</param>
/// <param name="EndTime">When the slot ends.</param>
/// <param name="RunwayIdentifiers">The runways affected by this slot.</param>
public record SlotDto(
    Guid Id,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers);
