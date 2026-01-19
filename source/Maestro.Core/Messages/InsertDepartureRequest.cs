using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record InsertDepartureRequest(
    string AirportIdentifier,
    string Callsign,
    string AircraftType,
    string DepartureAirport,
    IInsertFlightOptions Options)
    : IRequest, IRelayableRequest;
