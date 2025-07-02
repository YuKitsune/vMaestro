using Maestro.Core.Handlers;

namespace Maestro.Core.Messages;

public class SequenceMessage
{
    public required RunwayModeDto CurrentRunwayMode { get; init; }
    public required RunwayModeDto? NextRunwayMode { get; init; }
    public required DateTimeOffset RunwayModeChangeTime { get; init; }
    public required FlightMessage[] Flights { get; init; }
}