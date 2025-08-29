using MediatR;

namespace Maestro.Core.Messages;

public record MakePendingRequest(string AirportIdentifier, string Callsign) : IRequest;
