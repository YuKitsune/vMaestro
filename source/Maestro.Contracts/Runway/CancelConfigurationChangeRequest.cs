using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Runway;

[MessagePackObject]
public record CancelConfigurationChangeRequest(
    [property: Key(0)] string AirportIdentifier)
    : IRequest, IRelayableRequest ;
