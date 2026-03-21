using Maestro.Contracts.Connectivity;
using MediatR;
using MessagePack;

namespace Maestro.Contracts.Sessions;

[MessagePackObject]
public record UpdateWindRequest(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] WindDto SurfaceWind,
    [property: Key(2)] WindDto UpperWind,
    [property: Key(3)] bool ManualWind)
    : IRequest, IRelayableRequest;
