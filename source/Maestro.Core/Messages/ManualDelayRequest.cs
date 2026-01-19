using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record ManualDelayRequest(string AirportIdentifier, string Callsign, int MaximumDelayMinutes) : IRequest, IRelayableRequest;
