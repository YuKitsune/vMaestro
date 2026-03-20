using MediatR;

namespace Maestro.Contracts.Coordination;

public record SendCoordinationMessageRequest(
    string AirportIdentifier,
    DateTimeOffset Time,
    string Message,
    CoordinationDestination Destination)
    : IRequest;
