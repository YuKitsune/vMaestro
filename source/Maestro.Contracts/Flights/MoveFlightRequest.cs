using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record MoveFlightRequest(
    string AirportIdentifier,
    string Callsign,
    string[] RunwayIdentifiers,
    DateTimeOffset NewLandingTime)
    : IRequest, IRelayableRequest;
