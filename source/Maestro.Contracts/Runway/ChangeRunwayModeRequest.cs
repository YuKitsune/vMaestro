using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Runway;

[MessagePackObject]
public record ChangeRunwayModeRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] RunwayModeDto RunwayMode,
    [property: Key(2)] DateTimeOffset LastLandingTimeForOldMode,
    [property: Key(3)] DateTimeOffset FirstLandingTimeForNewMode)
    : IRequest, IRelayableRequest;
