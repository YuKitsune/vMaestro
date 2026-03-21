using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record ChangeFeederFixEstimateRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string Callsign,
    [property: Key(2)] DateTimeOffset NewFeederFixEstimate)
    : IRequest, IRelayableRequest;
