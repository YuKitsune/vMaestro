namespace Maestro.Core.Messages;

public class SequenceMessage
{
    public required FlightMessage[] Flights { get; init; }
}