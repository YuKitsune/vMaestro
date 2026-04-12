using Maestro.Contracts.Sessions;
using MessagePack;

namespace Maestro.Contracts.Connectivity;

[MessagePackObject]
public record InitializeConnectionResponse(
    [property: Key(0)] string ConnectionId,
    [property: Key(1)] string Environment,
    [property: Key(2)] string AirportIdentifier,
    [property: Key(3)] bool IsMaster,
    [property: Key(4)] SessionDto? Session,
    [property: Key(5)] PeerInfo[] ConnectedPeers);
