using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class SendCoordinationMessageRequestHandlerTests
{
    const string Version = "0.0.0";

    const string TestMessage = "Test coordination message";

    [Fact]
    public async Task WhenTheConnectionIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        var request = new SendCoordinationMessageRequest(
            "YSSY",
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Broadcast());

        var wrappedRequest = new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(connectionId, request);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny)).Returns(false);

        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
                .Handle(wrappedRequest, CancellationToken.None));

        exception.Message.ShouldBe($"Connection {connectionId} is not tracked");
    }

    [Fact]
    public async Task WhenSenderIsObserver_FailureIsReturned()
    {
        // Arrange
        const string connectionId = "observer-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";

        var observerConnection = new Connection(connectionId, Version, partition, airportIdentifier, "OBS", Role.Observer) { IsMaster = false };

        var request = new SendCoordinationMessageRequest(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Broadcast());

        var wrappedRequest = new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(connectionId, request);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = observerConnection;
                return true;
            }));

        var hubProxy = new Mock<IHubProxy>();

        // Act
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Observers cannot send coordination messages");
    }

    [Fact]
    public async Task WhenDestinationIsBroadcast_MessageIsSentToAllPeers()
    {
        // Arrange
        const string connectionId = "sender-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";
        const string senderCallsign = "ML-BIK_CTR";

        var senderConnection = new Connection(connectionId, Version, partition, airportIdentifier, senderCallsign, Role.Enroute) { IsMaster = false };
        var peer1 = new Connection("peer-1", Version, partition, airportIdentifier, "SY_FMP", Role.Flow) { IsMaster = true };
        var peer2 = new Connection("peer-2", Version, partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peers = new[] { peer1, peer2 };

        var request = new SendCoordinationMessageRequest(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Broadcast());

        var wrappedRequest = new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(connectionId, request);

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
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();

        // Sender should not receive their own message
        hubProxy.Verify(x => x.Send(
            senderConnection.Id,
            "CoordinationMessageReceived",
            It.IsAny<CoordinationMessageReceivedNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify peer1 receives the message with correct sender
        hubProxy.Verify(x => x.Send(
            peer1.Id,
            "CoordinationMessageReceived",
            It.Is<CoordinationMessageReceivedNotification>(n =>
                n.Sender == senderCallsign &&
                n.Message == TestMessage &&
                n.AirportIdentifier == airportIdentifier),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify peer2 receives the message with correct sender
        hubProxy.Verify(x => x.Send(
            peer2.Id,
            "CoordinationMessageReceived",
            It.Is<CoordinationMessageReceivedNotification>(n =>
                n.Sender == senderCallsign &&
                n.Message == TestMessage &&
                n.AirportIdentifier == airportIdentifier),
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
        const string senderCallsign = "ML-BIK_CTR";

        var senderConnection = new Connection(connectionId, Version, partition, airportIdentifier, senderCallsign, Role.Enroute) { IsMaster = false };
        var targetPeer = new Connection("target-peer", Version, partition, airportIdentifier, targetCallsign, Role.Approach) { IsMaster = false };
        var otherPeer = new Connection("other-peer", Version, partition, airportIdentifier, "SY_FMP", Role.Flow) { IsMaster = true };
        var peers = new[] { targetPeer, otherPeer };

        var request = new SendCoordinationMessageRequest(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Controller(targetCallsign));

        var wrappedRequest = new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(connectionId, request);

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
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();

        // Sender should not receive their own message
        hubProxy.Verify(x => x.Send(
            senderConnection.Id,
            "CoordinationMessageReceived",
            It.IsAny<CoordinationMessageReceivedNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Verify target peer receives the message
        hubProxy.Verify(x => x.Send(
            targetPeer.Id,
            "CoordinationMessageReceived",
            It.Is<CoordinationMessageReceivedNotification>(n =>
                n.Sender == senderCallsign &&
                n.Message == TestMessage &&
                n.AirportIdentifier == airportIdentifier),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify other peer does not receive the message
        hubProxy.Verify(x => x.Send(
            otherPeer.Id,
            "CoordinationMessageReceived",
            It.IsAny<CoordinationMessageReceivedNotification>(),
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

        var senderConnection = new Connection(connectionId, Version, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = false };
        var peer1 = new Connection("peer-1", Version, partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peer2 = new Connection("peer-2", Version, partition, airportIdentifier, "SY_FMP", Role.Flow) { IsMaster = true };
        var peers = new[] { peer1, peer2 };

        var request = new SendCoordinationMessageRequest(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            TestMessage,
            new CoordinationDestination.Controller(targetCallsign));

        var wrappedRequest = new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(connectionId, request);

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
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();

        hubProxy.Verify(x => x.Send(
            It.IsAny<string>(),
            "CoordinationMessageReceived",
            It.IsAny<CoordinationMessageReceivedNotification>(),
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

        var senderConnection = new Connection(connectionId, Version, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var peers = Array.Empty<Connection>();

        var request = new SendCoordinationMessageRequest(
            airportIdentifier,
            DateTimeOffset.UtcNow,
            message,
            new CoordinationDestination.Broadcast());

        var wrappedRequest = new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(connectionId, request);

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
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeEmpty();

        hubProxy.Verify(x => x.Send(
            It.IsAny<string>(),
            "CoordinationMessageReceived",
            It.IsAny<CoordinationMessageReceivedNotification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    SendCoordinationMessageRequestHandler GetHandler(
        IConnectionManager? connectionManager = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new SendCoordinationMessageRequestHandler(connectionManager, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
