using MediatR;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record PeerDisconnectedNotification(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string Callsign)
    : INotification;
