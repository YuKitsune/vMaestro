using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record ChangeApproachTypeRequest(string AirportIdentifier, string Callsign, string ApproachType) : IRequest, IRelayableRequest;
