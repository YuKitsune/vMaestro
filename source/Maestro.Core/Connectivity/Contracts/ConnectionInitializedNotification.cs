using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionInitializedNotification(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SessionMessage? Session,
    IReadOnlyList<PeerInfo> ConnectedPeers) : INotification;
