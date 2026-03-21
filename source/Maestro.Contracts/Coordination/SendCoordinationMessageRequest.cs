using MediatR;
using MessagePack;

namespace Maestro.Contracts.Coordination;

[MessagePackObject]
public record SendCoordinationMessageRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] DateTimeOffset Time,
    [property: Key(2)] string Message,
    [property: Key(3)] CoordinationDestination Destination)
    : IRequest;
