using MediatR;

namespace Maestro.Core.Messages;

public class SequenceUpdatedNotification(string airportIdentifier, SequenceMessage sequence) : INotification
{
    public string AirportIdentifier { get; } = airportIdentifier;
    public SequenceMessage Sequence { get; } = sequence;
}
