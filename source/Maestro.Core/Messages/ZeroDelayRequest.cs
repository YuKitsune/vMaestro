using MediatR;

namespace Maestro.Core.Messages;

public record ZeroDelayRequest(string AirportIdentifier, string Callsign) : IRequest, ISynchronizedMessage;
