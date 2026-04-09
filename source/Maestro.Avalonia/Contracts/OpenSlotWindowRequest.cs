using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenSlotWindowRequest(
    string AirportIdentifier,
    Guid? SlotId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers) : IRequest;
