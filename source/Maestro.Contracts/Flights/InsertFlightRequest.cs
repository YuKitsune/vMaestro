using Maestro.Contracts.Connectivity;
using MediatR;

namespace Maestro.Contracts.Flights;

public record InsertFlightRequest(
    string AirportIdentifier,
    string? Callsign,
    string? AircraftType,
    IInsertFlightOptions Options)
    : IRequest, IRelayableRequest;
