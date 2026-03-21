using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record MakePendingRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
