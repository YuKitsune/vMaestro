using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Slots;
using MessagePack;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents the current state of the arrival sequence.
/// </summary>
[MessagePackObject]
public class SequenceDto
{
    /// <summary>
    /// The currently active runway mode configuration.
    /// </summary>
    [Key(0)]
    public required RunwayModeDto CurrentRunwayMode { get; init; }

    /// <summary>
    /// The next scheduled runway mode, if a mode change is pending.
    /// </summary>
    [Key(1)]
    public required RunwayModeDto? NextRunwayMode { get; init; }

    /// <summary>
    /// The last scheduled landing time before the runway mode changes.
    /// </summary>
    [Key(2)]
    public required DateTimeOffset LastLandingTimeForCurrentMode { get; init; }

    /// <summary>
    /// The first scheduled landing time after the runway mode changes.
    /// </summary>
    [Key(3)]
    public required DateTimeOffset FirstLandingTimeForNextMode { get; init; }

    /// <summary>
    /// All flights currently in the sequence, ordered by landing time.
    /// </summary>
    [Key(4)]
    public required FlightDto[] Flights { get; init; }

    /// <summary>
    /// Time slots that restrict runway availability.
    /// </summary>
    [Key(5)]
    public required SlotDto[] Slots { get; init; }
}
