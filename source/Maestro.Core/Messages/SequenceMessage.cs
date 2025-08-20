using Maestro.Core.Handlers;

namespace Maestro.Core.Messages;

public class SequenceMessage
{
    public required string AirportIdentifier { get; init; }
    public required RunwayModeDto CurrentRunwayMode { get; init; }
    public required RunwayModeDto? NextRunwayMode { get; init; }
    public required DateTimeOffset LastLandingTimeForCurrentMode { get; init; }
    public required DateTimeOffset FirstLandingTimeForNextMode { get; init; }
    public required FlightMessage[] Flights { get; init; }
    public required string[] DesequencedFlights { get; init; }
    public required string[] LandedFlights { get; init; }
    public required string[] PendingFlights { get; init; }
    public required SlotMessage[] Slots { get; init; }
}

public record SlotMessage(
    Guid SlotId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers);
