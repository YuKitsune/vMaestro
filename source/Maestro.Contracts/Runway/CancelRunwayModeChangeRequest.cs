using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Runway;

[MessagePackObject]
public record CancelRunwayModeChangeRequest(
    [property: Key(0)] string AirportIdentifier)
    : IRequest, IRelayableRequest ;
