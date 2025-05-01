using MediatR;

namespace Maestro.Core.Messages;

public record RemoveResponse;
public record RemoveRequest(string AirportIdentifier, string Callsign) : IRequest<RemoveResponse>;