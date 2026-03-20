using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Sessions;

namespace Maestro.Core.Connectivity.Contracts;

public record InitializeConnectionResponse(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SessionDto? Session,
    PeerInfo[] ConnectedPeers);
