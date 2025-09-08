using MediatR;

namespace Maestro.Core.Messages;

public record DesequenceRequest(string AirportIdentifier, string Callsign) : IRequest, ISynchronizedMessage;
