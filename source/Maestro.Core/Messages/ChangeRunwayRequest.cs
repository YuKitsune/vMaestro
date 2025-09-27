using MediatR;

namespace Maestro.Core.Messages;

public record ChangeRunwayRequest(string AirportIdentifier, string Callsign, string RunwayIdentifier) : IRequest;
