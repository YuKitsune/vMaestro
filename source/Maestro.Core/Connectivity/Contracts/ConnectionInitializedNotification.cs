using Maestro.Core.Messages;
using MediatR;

namespace Maestro.Core.Connectivity.Contracts;

public record ConnectionInitializedNotification(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SequenceMessage? Sequence,
    IReadOnlyList<PeerInfo> ConnectedPeers) : INotification;
