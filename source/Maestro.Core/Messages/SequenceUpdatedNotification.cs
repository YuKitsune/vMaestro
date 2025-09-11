using MediatR;

namespace Maestro.Core.Messages;

// TODO: Split this up into multiple messages for different sequence changes

public record SequenceUpdatedNotification(
    string AirportIdentifier,
    SequenceMessage Sequence)
    : INotification;
