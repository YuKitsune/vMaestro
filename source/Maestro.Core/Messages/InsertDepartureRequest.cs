using MediatR;

namespace Maestro.Core.Messages;

// TODO: Consolidate with InsertPenidngRequest

public record InsertDepartureRequest(
    string AirportIdentifier,
    string Callsign,
    string AircraftType,
    string DepartureAirport,
    DateTimeOffset TakeOffTime)
    : IRequest;
