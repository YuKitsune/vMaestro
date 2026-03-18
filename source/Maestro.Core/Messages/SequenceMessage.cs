namespace Maestro.Core.Messages;

/// <summary>
/// Represents the current state of the arrival sequence.
/// </summary>
public class SequenceMessage
{
    /// <summary>
    /// The currently active runway mode configuration.
    /// </summary>
    public required RunwayModeDto CurrentRunwayMode { get; init; }

    /// <summary>
    /// The next scheduled runway mode, if a mode change is pending.
    /// </summary>
    public required RunwayModeDto? NextRunwayMode { get; init; }

    /// <summary>
    /// The last scheduled landing time before the runway mode changes.
    /// </summary>
    public required DateTimeOffset LastLandingTimeForCurrentMode { get; init; }

    /// <summary>
    /// The first scheduled landing time after the runway mode changes.
    /// </summary>
    public required DateTimeOffset FirstLandingTimeForNextMode { get; init; }

    /// <summary>
    /// All flights currently in the sequence, ordered by landing time.
    /// </summary>
    public required FlightMessage[] Flights { get; init; }

    /// <summary>
    /// Time slots that restrict runway availability.
    /// </summary>
    public required SlotMessage[] Slots { get; init; }
}

/// <summary>
/// Represents a time slot during which certain runways are unavailable.
/// </summary>
/// <param name="Id">The unique identifier for this slot.</param>
/// <param name="StartTime">When the slot begins.</param>
/// <param name="EndTime">When the slot ends.</param>
/// <param name="RunwayIdentifiers">The runways affected by this slot.</param>
public record SlotMessage(
    Guid Id,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers);
