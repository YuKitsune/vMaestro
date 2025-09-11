using MediatR;

namespace Maestro.Core.Messages;

public record MoveFlightRequest(
    string AirportIdentifier,
    string Callsign,
    string[] RunwayIdentifiers,
    DateTimeOffset NewLandingTime)
    : IRequest;
