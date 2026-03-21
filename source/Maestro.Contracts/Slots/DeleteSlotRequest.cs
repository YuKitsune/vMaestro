using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Slots;

[MessagePackObject]
public record DeleteSlotRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] Guid SlotId)
    : IRequest, IRelayableRequest;
