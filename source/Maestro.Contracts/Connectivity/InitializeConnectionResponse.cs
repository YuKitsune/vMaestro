using Maestro.Contracts.Sessions;

namespace Maestro.Contracts.Connectivity;

public record InitializeConnectionResponse(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SessionDto? Session,
    PeerInfo[] ConnectedPeers);
