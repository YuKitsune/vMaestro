using MediatR;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record PeerConnectedNotification(
    [property: Key(0)] string AirportIdentifier,
    [property: Key(1)] string Callsign,
    [property: Key(2)] Role Role)
    : INotification;
