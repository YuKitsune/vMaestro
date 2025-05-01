using MediatR;

namespace Maestro.Core.Messages;

public record MakePendingResponse;
public record MakePendingRequest(string AirportIdentifier, string Callsign) : IRequest<MakePendingResponse>;