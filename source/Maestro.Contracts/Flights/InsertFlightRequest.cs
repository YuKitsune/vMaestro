using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record InsertFlightRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string? Callsign,
    [property: Key(2)] string? AircraftType,
    [property: Key(3)] IInsertFlightOptions Options)
    : IRequest, IRelayableRequest;
