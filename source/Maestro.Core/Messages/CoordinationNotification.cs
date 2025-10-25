using MediatR;

namespace Maestro.Core.Messages;

public record CoordinationNotification(
    string AirportIdentifier,
    DateTimeOffset Time,
    string Message,
    CoordinationDestination Destination)
    : INotification;
