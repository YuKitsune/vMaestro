using MediatR;

namespace Maestro.Core.Messages;

public record SequenceInitializedNotification(string AirportIdentifier, SequenceMessage Sequence) : INotification;
