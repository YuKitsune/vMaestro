using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record ResumeSequencingRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
