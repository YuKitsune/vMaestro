using MediatR;

namespace Maestro.Core.Messages.Connectivity;

public record ConnectionInitializedNotification(
    string ConnectionId,
    string Partition,
    string AirportIdentifier,
    bool IsMaster,
    SequenceMessage? Sequence,
    IReadOnlyList<PeerInfo> ConnectedPeers) : INotification;
