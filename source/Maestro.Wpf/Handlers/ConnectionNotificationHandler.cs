using CommunityToolkit.Mvvm.Messaging;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using Maestro.Wpf.Messages;
using MediatR;

namespace Maestro.Wpf.Handlers;

public class ConnectionNotificationHandler(IPeerTracker peerTracker, ISessionManager sessionManager)
    : INotificationHandler<ConnectionReadyNotification>,
        INotificationHandler<ConnectionUnreadyNotification>,
        INotificationHandler<SessionConnectedNotification>,
        INotificationHandler<SessionReconnectingNotification>,
        INotificationHandler<SessionDisconnectedNotification>,
        INotificationHandler<PeerConnectedNotification>,
        INotificationHandler<PeerDisconnectedNotification>
{
    public Task Handle(ConnectionReadyNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "READY"));
        return Task.CompletedTask;
    }

    public Task Handle(ConnectionUnreadyNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "OFFLINE"));
        return Task.CompletedTask;
    }

    public Task Handle(SessionConnectedNotification notification, CancellationToken cancellationToken)
    {
        var flowIsOnline = peerTracker.IsFlowControllerOnline(notification.AirportIdentifier);
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(notification.Role, flowIsOnline)));
        return Task.CompletedTask;
    }

    public Task Handle(SessionReconnectingNotification notification, CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "RECONN"));
        return Task.CompletedTask;
    }

    public Task Handle(SessionDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.IsReady)
        {
            WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "READY"));
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, "OFFLINE"));
        }

        return Task.CompletedTask;
    }

    public async Task Handle(PeerConnectedNotification notification, CancellationToken cancellationToken)
    {
        var currentRole = await GetCurrentRole(notification.AirportIdentifier, cancellationToken);
        bool flowIsOnline = peerTracker.IsFlowControllerOnline(notification.AirportIdentifier);
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(currentRole, flowIsOnline)));
    }

    public async Task Handle(PeerDisconnectedNotification notification, CancellationToken cancellationToken)
    {
        var role = await GetCurrentRole(notification.AirportIdentifier, cancellationToken);
        var flowIsOnline = peerTracker.IsFlowControllerOnline(notification.AirportIdentifier);
        WeakReferenceMessenger.Default.Send(new ConnectionStatusChanged(notification.AirportIdentifier, GetStatus(role, flowIsOnline)));
    }

    string GetStatus(Role role, bool flowIsOnline) =>
        role switch
        {
            Role.Flow => "FLOW",
            Role.Enroute => flowIsOnline ? "ENR" : "ENR/FLOW",
            Role.Approach => flowIsOnline ? "APP" : "APP/FLOW",
            Role.Observer => "OBS",
            _ => throw new ArgumentOutOfRangeException()
        };

    async Task<Role> GetCurrentRole(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var lockedSession = await sessionManager.AcquireSession(airportIdentifier, cancellationToken);
        return lockedSession.Session.Role;
    }
}
