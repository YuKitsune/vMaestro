using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Sessions;
using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionInitializedNotification(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SessionDto? Session,
    PeerInfo[] ConnectedPeers) : INotification;
