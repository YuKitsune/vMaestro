using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Slots;

namespace Maestro.Contracts.Sessions;

/// <summary>
/// Represents the current state of the arrival sequence.
/// </summary>
public class SequenceDto
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
    public required FlightDto[] Flights { get; init; }

    /// <summary>
    /// Time slots that restrict runway availability.
    /// </summary>
    public required SlotDto[] Slots { get; init; }
}
