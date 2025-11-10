using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class ConnectRequestHandlerTests
{
    [Fact]
    public async Task WhenNoPeersExist_TheFirstConnectionBecomesTheMaster()
    {
        // Arrange
        const string connectionId = "connection-1";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var request = new ConnectRequest(partition, airportIdentifier, callsign, role);
        var wrappedRequest = new RequestContextWrapper<ConnectRequest>(connectionId, request);

        var connection = new Connection(connectionId, partition, airportIdentifier, callsign, role);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([]);
        connectionManager.Setup(x => x.Add(connectionId, partition, airportIdentifier, callsign, role)).Returns(connection);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        connection.IsMaster.ShouldBeTrue();

        hubProxy.Verify(x => x.Send(
            connectionId,
            "ConnectionInitialized",
            It.Is<ConnectionInitializedNotification>(n =>
                n.ConnectionId == connectionId &&
                n.IsMaster == true &&
                n.ConnectedPeers.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenNoPeersExist_AndTheFirstConnectionIsAnObserver_TheyAreNotAssignedAsTheMaster()
    {
        // Arrange
        const string connectionId = "connection-1";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "AA_OBS";
        const Role role = Role.Observer;

        var request = new ConnectRequest(partition, airportIdentifier, callsign, role);
        var wrappedRequest = new RequestContextWrapper<ConnectRequest>(connectionId, request);

        var connection = new Connection(connectionId, partition, airportIdentifier, callsign, role);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([]);
        connectionManager.Setup(x => x.Add(connectionId, partition, airportIdentifier, callsign, role)).Returns(connection);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        connection.IsMaster.ShouldBeFalse();

        hubProxy.Verify(x => x.Send(
            connectionId,
            "ConnectionInitialized",
            It.Is<ConnectionInitializedNotification>(n =>
                n.ConnectionId == connectionId &&
                n.IsMaster == false &&
                n.ConnectedPeers.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenPeersExist_TheyAreNotified()
    {
        // Arrange
        const string connectionId = "connection-2";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var request = new ConnectRequest(partition, airportIdentifier, callsign, role);
        var wrappedRequest = new RequestContextWrapper<ConnectRequest>(connectionId, request);

        var existingConnection = new Connection("connection-1", partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = true };
        var newConnection = new Connection(connectionId, partition, airportIdentifier, callsign, role);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([existingConnection]);
        connectionManager.Setup(x => x.Add(connectionId, partition, airportIdentifier, callsign, role)).Returns(newConnection);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            existingConnection.Id,
            "PeerConnected",
            It.Is<PeerConnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign &&
                n.Role == role),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenPeersExist_AndAFlowControllerJoins_FlowControllerBecomesMaster()
    {
        // Arrange
        const string connectionId = "connection-2";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "SY_FMP";
        const Role role = Role.Flow;

        var request = new ConnectRequest(partition, airportIdentifier, callsign, role);
        var wrappedRequest = new RequestContextWrapper<ConnectRequest>(connectionId, request);

        var existingConnection = new Connection("connection-1", partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var newConnection = new Connection(connectionId, partition, airportIdentifier, callsign, role);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([existingConnection]);
        connectionManager.Setup(x => x.Add(connectionId, partition, airportIdentifier, callsign, role)).Returns(newConnection);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        existingConnection.IsMaster.ShouldBeFalse();
        newConnection.IsMaster.ShouldBeTrue();

        // OwnershipRevoked should be sent to the OLD master (existingConnection)
        hubProxy.Verify(x => x.Send(
            existingConnection.Id,
            "OwnershipRevoked",
            It.Is<OwnershipRevokedNotification>(n => n.AirportIdentifier == airportIdentifier),
            It.IsAny<CancellationToken>()), Times.Once);

        // PeerConnected should be sent to the existing connection
        hubProxy.Verify(x => x.Send(
            existingConnection.Id,
            "PeerConnected",
            It.Is<PeerConnectedNotification>(n =>
                n.AirportIdentifier == airportIdentifier &&
                n.Callsign == callsign &&
                n.Role == role),
            It.IsAny<CancellationToken>()), Times.Once);

        // ConnectionInitialized with IsMaster=true should be sent to the NEW Flow controller
        hubProxy.Verify(x => x.Send(
            connectionId,
            "ConnectionInitialized",
            It.Is<ConnectionInitializedNotification>(n =>
                n.ConnectionId == connectionId &&
                n.IsMaster == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenConnecting_AndACachedSequenceExists_ItIsReturnedToTheClient()
    {
        // Arrange
        const string connectionId = "connection-1";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var request = new ConnectRequest(partition, airportIdentifier, callsign, role);
        var wrappedRequest = new RequestContextWrapper<ConnectRequest>(connectionId, request);

        var connection = new Connection(connectionId, partition, airportIdentifier, callsign, role);
        var cachedSequence = new SequenceMessage
        {
            AirportIdentifier = null,
            CurrentRunwayMode = null,
            NextRunwayMode = null,
            LastLandingTimeForCurrentMode = default,
            FirstLandingTimeForNextMode = default,
            Flights = [],
            PendingFlights = [],
            DeSequencedFlights = [],
            Slots = [],
            DummyCounter = 0
        };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([]);
        connectionManager.Setup(x => x.Add(connectionId, partition, airportIdentifier, callsign, role)).Returns(connection);

        var sequenceCache = new SequenceCache();
        sequenceCache.Set(partition, airportIdentifier, cachedSequence);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager.Object, sequenceCache, hubProxy.Object).Handle(wrappedRequest, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            connectionId,
            "ConnectionInitialized",
            It.Is<ConnectionInitializedNotification>(n =>
                n.ConnectionId == connectionId &&
                n.Sequence == cachedSequence),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    ConnectRequestHandler GetHandler(
        IConnectionManager? connectionManager = null,
        SequenceCache? sequenceCache = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        sequenceCache ??= new SequenceCache();
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new ConnectRequestHandler(connectionManager, sequenceCache, hubProxy, logger);
    }
}
