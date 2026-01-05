using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class ClientDisconnectedNotificationHandlerTests
{
    const string Version = "0.0.0";

    [Fact]
    public async Task WhenConnectionIsUntracked_NoExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        var notification = new ClientDisconnectedNotification(connectionId);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny)).Returns(false);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert - Handler gracefully returns without throwing
        connectionManager.Verify(x => x.Remove(It.IsAny<Connection>()), Times.Never);
        hubProxy.Verify(x => x.Send(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenLeavingTheSession_PeersAreNotified()
    {
        // Arrange
        const string connectionId = "connection-1";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "SY_APP";
        const Role role = Role.Approach;

        var notification = new ClientDisconnectedNotification(connectionId);

        var disconnectingConnection = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = false };
        var peerConnection = new Connection("peer-1", Version, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = disconnectingConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(disconnectingConnection)).Returns([peerConnection]);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert
        connectionManager.Verify(x => x.Remove(disconnectingConnection), Times.Once);

        hubProxy.Verify(x => x.Send(
            peerConnection.Id,
            "PeerDisconnected",
            It.Is<PeerDisconnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenMasterLeavesTheSession_AndAnotherFlowControllerExists_FlowControllerIsPromoted()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "SY_FMP";
        const Role role = Role.Enroute;

        var notification = new ClientDisconnectedNotification(connectionId);

        var primaryFlowController = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = true };
        var secondaryFlowController = new Connection("flow-connection", Version, partition, airportIdentifier, "SY__FMP", Role.Flow) { IsMaster = false };
        var otherPeer = new Connection("other-connection", Version, partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = primaryFlowController;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(primaryFlowController)).Returns([otherPeer, secondaryFlowController]);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert
        primaryFlowController.IsMaster.ShouldBeTrue();
        otherPeer.IsMaster.ShouldBeFalse();

        hubProxy.Verify(x => x.Send(
            secondaryFlowController.Id,
            "OwnershipGranted",
            It.Is<OwnershipGrantedNotification>(n => n.AirportIdentifier == airportIdentifier),
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            secondaryFlowController.Id,
            "PeerDisconnected",
            It.Is<PeerDisconnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign),
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            otherPeer.Id,
            "PeerDisconnected",
            It.Is<PeerDisconnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenMasterLeavesTheSession_AndNoFlowControllerExists_NextAvailableConnectionIsPromoted()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var notification = new ClientDisconnectedNotification(connectionId);

        var masterConnection = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = true };
        var nextMaster = new Connection("next-connection", Version, partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var observer = new Connection("observer-connection", Version, partition, airportIdentifier, "AA_OBS", Role.Observer) { IsMaster = false };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(masterConnection)).Returns([nextMaster, observer]);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert
        nextMaster.IsMaster.ShouldBeTrue();
        observer.IsMaster.ShouldBeFalse();

        hubProxy.Verify(x => x.Send(
            nextMaster.Id,
            "OwnershipGranted",
            It.Is<OwnershipGrantedNotification>(n => n.AirportIdentifier == airportIdentifier),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenMasterLeavesTheSession_AndOnlyObserversRemain_NoNewMasterIsPromoted()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var notification = new ClientDisconnectedNotification(connectionId);

        var masterConnection = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = true };
        var observer1 = new Connection("observer-1", Version, partition, airportIdentifier, "AA_OBS", Role.Observer) { IsMaster = false };
        var observer2 = new Connection("observer-2", Version, partition, airportIdentifier, "BB_OBS", Role.Observer) { IsMaster = false };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(masterConnection)).Returns([observer1, observer2]);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert
        observer1.IsMaster.ShouldBeFalse();
        observer2.IsMaster.ShouldBeFalse();

        hubProxy.Verify(x => x.Send(
            It.IsAny<string>(),
            "OwnershipGranted",
            It.IsAny<OwnershipGrantedNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        hubProxy.Verify(x => x.Send(
            observer1.Id,
            "PeerDisconnected",
            It.Is<PeerDisconnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign),
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            observer2.Id,
            "PeerDisconnected",
            It.Is<PeerDisconnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenLastConnectionDisconnects_AndNoPeersRemain_SessionCacheIsCleared()
    {
        // Arrange
        const string connectionId = "last-connection";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "SY_APP";
        const Role role = Role.Approach;

        var notification = new ClientDisconnectedNotification(connectionId);

        var lastConnection = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = true };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = lastConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(lastConnection)).Returns([]);

        var sessionCache = new SessionCache();
        sessionCache.Set(partition, airportIdentifier, new SessionMessage
        {
            AirportIdentifier = null,
            PendingFlights = [],
            DeSequencedFlights = [],
            DummyCounter = 0,
            Sequence = new SequenceMessage
            {
                CurrentRunwayMode = null,
                NextRunwayMode = null,
                LastLandingTimeForCurrentMode = default,
                FirstLandingTimeForNextMode = default,
                Flights = [],
                Slots = [],
            }
        });

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, sessionCache: sessionCache, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert
        sessionCache.Get(partition, airportIdentifier).ShouldBeNull();
    }

    [Fact]
    public async Task WhenLastNonObserverConnectionDisconnects_AndOnlyObserversRemain_SessionCacheIsCleared()
    {
        // Arrange
        const string connectionId = "last-atc-connection";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "SY_APP";
        const Role role = Role.Approach;

        var notification = new ClientDisconnectedNotification(connectionId);

        var lastAtcConnection = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = true };
        var observer1 = new Connection("observer-1", Version, partition, airportIdentifier, "AA_OBS", Role.Observer) { IsMaster = false };
        var observer2 = new Connection("observer-2", Version, partition, airportIdentifier, "BB_OBS", Role.Observer) { IsMaster = false };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = lastAtcConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(lastAtcConnection)).Returns([observer1, observer2]);

        var sessionCache = new SessionCache();
        sessionCache.Set(partition, airportIdentifier, new SessionMessage
        {
            AirportIdentifier = null,
            PendingFlights = [],
            DeSequencedFlights = [],
            DummyCounter = 0,
            Sequence = new SequenceMessage
            {
                CurrentRunwayMode = null,
                NextRunwayMode = null,
                LastLandingTimeForCurrentMode = default,
                FirstLandingTimeForNextMode = default,
                Flights = [],
                Slots = [],
            }
        });

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, sessionCache: sessionCache, hubProxy: hubProxy.Object)
            .Handle(notification, CancellationToken.None);

        // Assert
        sessionCache.Get(partition, airportIdentifier).ShouldBeNull();
    }

    ClientDisconnectedNotificationHandler GetHandler(
        IConnectionManager? connectionManager = null,
        SessionCache? sessionCache = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        sessionCache ??= new SessionCache();
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new ClientDisconnectedNotificationHandler(connectionManager, sessionCache, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
