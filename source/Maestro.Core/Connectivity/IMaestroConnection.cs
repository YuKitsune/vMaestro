using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Connectivity;

public interface IMaestroConnection
{
    bool IsConnected { get; }
    bool IsMaster { get; }
    Role Role { get; }
    IReadOnlyList<PeerInfo> Peers { get; }
    string Partition { get; }

    Task Start(string callsign, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);

    Task Invoke<T>(T message, CancellationToken cancellationToken)
        where T : class, IRequest;

    Task Send<T>(T message, CancellationToken cancellationToken)
        where T : class, INotification;
}
