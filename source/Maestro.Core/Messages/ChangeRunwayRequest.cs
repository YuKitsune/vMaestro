using MediatR;

namespace Maestro.Core.Messages;

public record ChangeRunwayRequest(
    string AirportIdentifier,
    string Callsign,
    RunwayDto Runway // TODO: Undo this. We just want to change the runway. The whole DTO is too much info.
    ) : IRequest;
