using Maestro.Core.Handlers;

namespace Maestro.Core.Messages;

public class SlotDto
{
    public required string Identifier { get; init; }
    public required FlightMessage? Flight { get; init; }
    public required string RunwayIdentifier { get; init; }
    public required bool Reserved { get; init; }
    public required DateTimeOffset Time { get; init; }
}

public class SlotBasedSequenceDto
{
    public required string AirportIdentifier { get; init; }
    public required RunwayModeDto CurrentRunwayMode { get; init; }
    public RunwayModeDto? NextRunwayMode { get; init; }
    public DateTimeOffset RunwayModeChangeTime { get; init; }
    public required string[] PendingFlights { get; init; }
    public required string[] DesequencedFlights { get; init; }
    public required string[] LandedFlights { get; init; }
    public required SlotDto[] Slots { get; init; }
}
