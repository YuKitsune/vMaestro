using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenSlotWindowRequest(
    string AirportIdentifier,
    Guid? SlotId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers) : IRequest;
