using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record ChangeFeederFixEstimateRequest(
    string AirportIdentifier,
    string Callsign,
    DateTimeOffset NewFeederFixEstimate)
    : IRequest, IRelayableRequest;
