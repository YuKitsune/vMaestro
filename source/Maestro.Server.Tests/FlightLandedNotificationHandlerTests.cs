using Maestro.Contracts.Connectivity;
using Maestro.Contracts.Flights;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class FlightLandedNotificationHandlerTests
{
    const string Version = "0.0.0";

    [Fact]
    public async Task WhenTheConnectionIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        var flightLandedNotification = new FlightLandedNotification(
            "YSSY",
            "QFA123",
            DateTimeOffset.UtcNow);

        var wrappedNotification = new NotificationContextWrapper<FlightLandedNotification>(connectionId, flightLandedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny)).Returns(false);

        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe($"Connection {connectionId} is not tracked");
    }

    [Fact]
    public async Task WhenTheConnectionIsTheMaster_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "master-connection";
        var flightLandedNotification = new FlightLandedNotification(
            "YSSY",
            "QFA123",
            DateTimeOffset.UtcNow);

        var wrappedNotification = new NotificationContextWrapper<FlightLandedNotification>(connectionId, flightLandedNotification);

        var masterConnection = new Connection(connectionId, Version, "partition-1", "YSSY", "ML-BIK_CTR", Role.Enroute) { IsMaster = true };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));

        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe("Cannot relay to master");
    }

    [Fact]
    public async Task WhenNoMasterFound_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "slave-connection";
        var flightLandedNotification = new FlightLandedNotification(
            "YSSY",
            "QFA123",
            DateTimeOffset.UtcNow);

        var wrappedNotification = new NotificationContextWrapper<FlightLandedNotification>(connectionId, flightLandedNotification);

        var slaveConnection = new Connection(connectionId, Version, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false };
        var peerConnections = new[]
        {
            new Connection("peer-1", Version, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false },
            new Connection("peer-2", Version, "partition-1", "YSSY", "AA_OBS", Role.Observer) { IsMaster = false }
        };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(slaveConnection)).Returns(peerConnections);

        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe("No master found");
    }

    [Fact]
    public async Task FlightLandedNotificationsAreRelayedToTheMaster()
    {
        // Arrange
        const string connectionId = "slave-connection";
        const string masterConnectionId = "master-connection";
        var flightLandedNotification = new FlightLandedNotification(
            "YSSY",
            "QFA123",
            DateTimeOffset.UtcNow);

        var wrappedNotification = new NotificationContextWrapper<FlightLandedNotification>(connectionId, flightLandedNotification);

        var slaveConnection = new Connection(connectionId, Version, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false };
        var masterConnection = new Connection(masterConnectionId, Version, "partition-1", "YSSY", "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var peerConnections = new[] { masterConnection };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(slaveConnection)).Returns(peerConnections);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            masterConnectionId,
            "FlightLanded",
            flightLandedNotification,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenMultiplePeersExist_OnlyMasterReceivesFlightLandedNotification()
    {
        // Arrange
        const string connectionId = "slave-connection";
        const string masterConnectionId = "master-connection";
        var flightLandedNotification = new FlightLandedNotification(
            "YSSY",
            "QFA123",
            DateTimeOffset.UtcNow);

        var wrappedNotification = new NotificationContextWrapper<FlightLandedNotification>(connectionId, flightLandedNotification);

        var slaveConnection = new Connection(connectionId, Version, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false };
        var masterConnection = new Connection(masterConnectionId, Version, "partition-1", "YSSY", "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var otherPeer = new Connection("other-peer", Version, "partition-1", "YSSY", "AA_OBS", Role.Observer) { IsMaster = false };
        var peerConnections = new[] { masterConnection, otherPeer };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(slaveConnection)).Returns(peerConnections);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            masterConnectionId,
            "FlightLanded",
            flightLandedNotification,
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            otherPeer.Id,
            "FlightLanded",
            It.IsAny<FlightLandedNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    FlightLandedNotificationHandler GetHandler(
        IConnectionManager? connectionManager = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new FlightLandedNotificationHandler(connectionManager, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
