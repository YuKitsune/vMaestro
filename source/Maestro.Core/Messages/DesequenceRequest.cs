using MediatR;

namespace Maestro.Core.Messages;

public record DesequenceResponse;
public record DesequenceRequest(string AirportIdentifier, string Callsign) : IRequest<DesequenceResponse>;