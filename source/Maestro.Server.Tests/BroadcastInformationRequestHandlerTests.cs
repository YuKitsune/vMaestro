using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class BroadcastInformationRequestHandlerTests
{
    [Fact]
    public async Task WhenTheConnectionIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        var informationNotification = new InformationNotification("YSSY", DateTimeOffset.Now, "Test message");
        var wrappedNotification = new NotificationContextWrapper<InformationNotification>(connectionId, informationNotification);

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
    public async Task WhenTheConnectionIsNotTheMaster_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "slave-connection";
        var informationNotification = new InformationNotification("YSSY", DateTimeOffset.Now, "Test message");
        var wrappedNotification = new NotificationContextWrapper<InformationNotification>(connectionId, informationNotification);

        var slaveConnection = new Connection(connectionId, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));

        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe("Only the master can broadcast information");
    }

    [Fact]
    public async Task InformationMessagesAreBroadcastToAllPeers()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var informationNotification = new InformationNotification(airportIdentifier, DateTimeOffset.Now, "Important announcement");
        var wrappedNotification = new NotificationContextWrapper<InformationNotification>(connectionId, informationNotification);

        var masterConnection = new Connection(connectionId, partition, airportIdentifier, callsign, role) { IsMaster = true };
        var peer1 = new Connection("peer-1", partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peer2 = new Connection("peer-2", partition, airportIdentifier, "AA_OBS", Role.Observer) { IsMaster = false };
        var peers = new[] { peer1, peer2 };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(masterConnection)).Returns(peers);

        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        hubProxy.Verify(x => x.Send(
            peer1.Id,
            "Information",
            informationNotification,
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            peer2.Id,
            "Information",
            informationNotification,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    BroadcastInformationRequestHandler GetHandler(
        IConnectionManager? connectionManager = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new BroadcastInformationRequestHandler(connectionManager, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
