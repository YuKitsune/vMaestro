using MediatR;

namespace Maestro.Core.Messages;

public record ManualDelayRequest(string AirportIdentifier, string Callsign, int MaximumDelayMinutes) : IRequest;
