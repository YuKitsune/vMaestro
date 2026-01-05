using Maestro.Core.Sessions;

namespace Maestro.Core.Connectivity.Contracts;

public record InitializeConnectionResponse(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SessionMessage? Session,
    PeerInfo[] ConnectedPeers);
