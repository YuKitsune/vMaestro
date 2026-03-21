using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record RemoveRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
