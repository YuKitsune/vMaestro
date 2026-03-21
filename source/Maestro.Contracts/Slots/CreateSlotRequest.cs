using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Slots;

[MessagePackObject]
public record CreateSlotRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] DateTimeOffset StartTime,
    [property: Key(2)] DateTimeOffset EndTime,
    [property: Key(3)] string[] RunwayIdentifiers)
    : IRequest, IRelayableRequest;
