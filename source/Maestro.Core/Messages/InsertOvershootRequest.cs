using MediatR;

namespace Maestro.Core.Messages;

public record InsertOvershootRequest(
    string AirportIdentifier,
    string? Callsign,
    IInsertFlightOptions Options)
    : IRequest;
