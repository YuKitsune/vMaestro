using MediatR;

namespace Maestro.Core.Messages;

public record SendCoordinationMessageRequest(
    string AirportIdentifier,
    DateTimeOffset Time,
    string Message,
    CoordinationDestination Destination)
    : IRequest;
