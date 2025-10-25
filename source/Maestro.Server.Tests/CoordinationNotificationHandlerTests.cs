using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class CoordinationNotificationHandlerTests
{
    const string TestMessage = "Test coordination message";

    [Fact]
    public async Task WhenTheConnectionIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        var coordinationNotification = new CoordinationNotification(
            "YSSY",
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Broadcast());

        var wrappedNotification = new NotificationContextWrapper<CoordinationNotification>(connectionId, coordinationNotification);

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
    public async Task WhenDestinationIsBroadcast_MessageIsSentToAllPeers()
    {
        // Arrange
        const string connectionId = "sender-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";

        var senderConnection = new Connection(connectionId, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = false };
        var peer1 = new Connection("peer-1", partition, airportIdentifier, "SY_FMP", Role.Flow) { IsMaster = true };
        var peer2 = new Connection("peer-2", partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peers = new[] { peer1, peer2 };

        var coordinationNotification = new CoordinationNotification(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Broadcast());

        var wrappedNotification = new NotificationContextWrapper<CoordinationNotification>(connectionId, coordinationNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = senderConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(senderConnection)).Returns(peers);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            senderConnection.Id,
            "Coordination",
            coordinationNotification,
            It.IsAny<CancellationToken>()), Times.Never);

        hubProxy.Verify(x => x.Send(
            peer1.Id,
            "Coordination",
            coordinationNotification,
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            peer2.Id,
            "Coordination",
            coordinationNotification,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenDestinationIsSpecificController_MessageIsSentOnlyToThatController()
    {
        // Arrange
        const string connectionId = "sender-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";
        const string targetCallsign = "SY_APP";

        var senderConnection = new Connection(connectionId, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = false };
        var targetPeer = new Connection("target-peer", partition, airportIdentifier, targetCallsign, Role.Approach) { IsMaster = false };
        var otherPeer = new Connection("other-peer", partition, airportIdentifier, "SY_FMP", Role.Flow) { IsMaster = true };
        var peers = new[] { targetPeer, otherPeer };

        var coordinationNotification = new CoordinationNotification(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Controller(targetCallsign));

        var wrappedNotification = new NotificationContextWrapper<CoordinationNotification>(connectionId, coordinationNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = senderConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(senderConnection)).Returns(peers);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            senderConnection.Id,
            "Coordination",
            It.IsAny<CoordinationNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        hubProxy.Verify(x => x.Send(
            targetPeer.Id,
            "Coordination",
            coordinationNotification,
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            otherPeer.Id,
            "Coordination",
            It.IsAny<CoordinationNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenDestinationIsSpecificControllerButNotFound_NoMessageIsSent()
    {
        // Arrange
        const string connectionId = "sender-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";
        const string targetCallsign = "UNKNOWN_CTR";

        var senderConnection = new Connection(connectionId, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = false };
        var peer1 = new Connection("peer-1", partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peer2 = new Connection("peer-2", partition, airportIdentifier, "SY_FMP", Role.Flow) { IsMaster = true };
        var peers = new[] { peer1, peer2 };

        var coordinationNotification = new CoordinationNotification(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Controller(targetCallsign));

        var wrappedNotification = new NotificationContextWrapper<CoordinationNotification>(connectionId, coordinationNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = senderConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(senderConnection)).Returns(peers);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            It.IsAny<string>(),
            "Coordination",
            It.IsAny<CoordinationNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenBroadcastingWithNoPeers_NoMessageIsSent()
    {
        // Arrange
        const string connectionId = "sender-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";
        const string message = "WX Dev have commenced.";

        var senderConnection = new Connection(connectionId, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var peers = Array.Empty<Connection>();

        var coordinationNotification = new CoordinationNotification(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            message,
            new CoordinationDestination.Broadcast());

        var wrappedNotification = new NotificationContextWrapper<CoordinationNotification>(connectionId, coordinationNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = senderConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(senderConnection)).Returns(peers);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            It.IsAny<string>(),
            "Coordination",
            It.IsAny<CoordinationNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    CoordinationNotificationHandler GetHandler(
        IConnectionManager? connectionManager = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new CoordinationNotificationHandler(connectionManager, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
