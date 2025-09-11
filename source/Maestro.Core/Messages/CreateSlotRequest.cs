using MediatR;

namespace Maestro.Core.Messages;

public record CreateSlotRequest(
    string AirportIdentifier,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers)
    : IRequest;
