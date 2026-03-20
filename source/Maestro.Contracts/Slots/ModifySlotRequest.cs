using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Slots;

public record ModifySlotRequest(
    string AirportIdentifier,
    Guid SlotId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime)
    : IRequest, IRelayableRequest;
