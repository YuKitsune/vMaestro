using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record SwapFlightsRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string FirstFlightCallsign,
    [property: Key(2)] string SecondFlightCallsign)
    : IRequest, IRelayableRequest;
