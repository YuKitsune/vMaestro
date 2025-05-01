using MediatR;

namespace Maestro.Core.Messages;

public record ZeroDelayResponse;
public record ZeroDelayRequest(string AirportIdentifier, string Callsign) : IRequest<ZeroDelayResponse>;