using MediatR;

namespace Maestro.Core.Messages;

public record SequenceUpdatedNotification(
    string AirportIdentifier,
    SequenceMessage Sequence)
    : INotification, ISynchronizedMessage;
