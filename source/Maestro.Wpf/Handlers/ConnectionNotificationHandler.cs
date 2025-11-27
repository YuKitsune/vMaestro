using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Connectivity;
using Maestro.Core.Connectivity.Contracts;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class ConnectionNotificationHandler(IMaestroConnectionManager connectionManager)
    : INotificationHandler<ConnectionCreatedNotification>,
        INotificationHandler<ConnectionStartedNotification>,
        INotificationHandler<ReconnectingNotification>,
        INotificationHandler<ReconnectedNotification>,
        INotificationHandler<ConnectionStoppedNotification>,
        INotificationHandler<ConnectionDestroyedNotification>,
        INotificationHandler<PeerConnectedNotification>,
        INotificationHandler<PeerDisconnectedNotification>
{
    public Task Handle(ConnectionCreatedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "READY"));
        return Task.CompletedTask;
    }

    public Task Handle(ConnectionStartedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection))
            return Task.CompletedTask;

        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(connection)));
        return Task.CompletedTask;
    }

    public Task Handle(ReconnectingNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "RECONN"));
        return Task.CompletedTask;
    }

    public Task Handle(ReconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection))
            return Task.CompletedTask;

        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(connection)));
        return Task.CompletedTask;
    }

    public Task Handle(ConnectionStoppedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "READY"));
        return Task.CompletedTask;
    }

    public Task Handle(ConnectionDestroyedNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "OFFLINE"));
        return Task.CompletedTask;
    }

    public Task Handle(PeerConnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection))
            return Task.CompletedTask;

        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(connection!)));
        return Task.CompletedTask;
    }

    public Task Handle(PeerDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (!connectionManager.TryGetConnection(notification.AirportIdentifier, out var connection))
            return Task.CompletedTask;

        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(connection!)));
        return Task.CompletedTask;
    }

    string GetStatus(IMaestroConnection connection)
    {
        var flowIsOnline = connection.Peers.Any(p => p.Role == Role.Flow);

        return connection.Role switch
        {
            Role.Flow => "FLOW",
            Role.Enroute => flowIsOnline ? "ENR" : "ENR/FLOW",
            Role.Approach => flowIsOnline ? "APP" : "APP/FLOW",
            Role.Observer => "OBS",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
