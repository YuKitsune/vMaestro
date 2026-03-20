using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record DesequenceRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
