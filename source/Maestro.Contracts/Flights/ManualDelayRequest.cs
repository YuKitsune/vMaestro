using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Flights;

[MessagePackObject]
public record ManualDelayRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string Callsign,
    [property: Key(2)] int MaximumDelayMinutes)
    : IRequest, IRelayableRequest;
