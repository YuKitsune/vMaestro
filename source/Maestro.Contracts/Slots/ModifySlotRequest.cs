using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Slots;

[MessagePackObject]
public record ModifySlotRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] Guid SlotId,
    [property: Key(2)] DateTimeOffset StartTime,
    [property: Key(3)] DateTimeOffset EndTime)
    : IRequest, IRelayableRequest;
