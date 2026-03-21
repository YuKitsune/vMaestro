using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record CleanUpFlightsRequest(
    [property: Key(0)] string AirportIdentifier)
    : IRequest;
