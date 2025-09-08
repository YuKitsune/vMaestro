using MediatR;

namespace Maestro.Core.Messages;

public record ModifySlotRequest(
    string AirportIdentifier,
    Guid SlotId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime)
    : IRequest, ISynchronizedMessage;
