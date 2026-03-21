using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record ChangeRunwayRequest(string AirportIdentifier, string Callsign, string RunwayIdentifier) : IRequest, IRelayableRequest;
