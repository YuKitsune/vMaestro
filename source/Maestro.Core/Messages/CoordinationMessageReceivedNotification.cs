using MediatR;

namespace Maestro.Core.Messages;

public record CoordinationMessageReceivedNotification(
    string AirportIdentifier,
    DateTimeOffset Time,
    string Sender,
    string Message,
    CoordinationDestination Destination)
    : INotification;
