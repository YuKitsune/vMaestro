using MediatR;

namespace Maestro.Core.Messages;

// TODO: Find a better name.
// Could make it a request, but there's issues with locking inside of handlers etc.

public record CoordinationMessageSentNotification(
    string AirportIdentifier,
    string Message,
    CoordinationDestination Destination)
    : INotification;
