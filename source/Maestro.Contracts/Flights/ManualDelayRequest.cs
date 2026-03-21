using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record ManualDelayRequest(string AirportIdentifier, string Callsign, int MaximumDelayMinutes) : IRequest, IRelayableRequest;
