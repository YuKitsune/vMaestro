using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record MakeStableRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
