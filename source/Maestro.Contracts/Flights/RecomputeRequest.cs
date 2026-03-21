using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record RecomputeRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string Callsign)
    : IRequest, IRelayableRequest;
