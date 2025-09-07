using MediatR;

namespace Maestro.Core.Messages;

public record SequenceTerminatedNotification(string AirportIdentifier) : INotification;
