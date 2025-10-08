using MediatR;

namespace Maestro.Core.Messages;

public record InsertPendingRequest(
    string AirportIdentifier,
    string Callsign,
    string AircraftType,
    IInsertFlightOptions Options)
    : IRequest;
