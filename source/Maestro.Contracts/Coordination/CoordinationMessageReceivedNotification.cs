using MediatR;

namespace Maestro.Contracts.Coordination;

public record CoordinationMessageReceivedNotification(
    string AirportIdentifier,
    DateTimeOffset Time,
    string Sender,
    string Message,
    CoordinationDestination Destination)
    : INotification;
