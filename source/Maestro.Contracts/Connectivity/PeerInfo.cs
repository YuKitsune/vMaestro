using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record PeerInfo(
    [property: Key(0)] string Callsign,
    [property: Key(1)] Role Role);
