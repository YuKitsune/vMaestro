using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Slots;

public record CreateSlotRequest(
    string AirportIdentifier,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string[] RunwayIdentifiers)
    : IRequest, IRelayableRequest;
