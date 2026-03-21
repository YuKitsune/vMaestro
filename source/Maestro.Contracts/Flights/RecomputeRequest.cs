using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record RecomputeRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
