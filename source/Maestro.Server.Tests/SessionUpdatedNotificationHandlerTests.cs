using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class SessionUpdatedNotificationHandlerTests
{
    [Fact]
    public async Task WhenTheClientIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        const string airportIdentifier = "YSSY";

        var sessionMessage = new SessionMessage
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
        };

        var sessionUpdatedNotification = new SessionUpdatedNotification(airportIdentifier, sessionMessage);
        var wrappedNotification = new NotificationContextWrapper<SessionUpdatedNotification>(connectionId, sessionUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny)).Returns(false);

        var sessionCache = new SessionCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, sessionCache: sessionCache, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe($"Connection {connectionId} is not tracked");
    }

    [Fact]
    public async Task WhenTheAirportIdentifiersMismatch_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string connectionAirport = "YSSY";
        const string notificationAirport = "YMML";
        const string partition = "partition-1";

        var masterConnection = new Connection(connectionId, partition, connectionAirport, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };

        var sessionMessage = new SessionMessage
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
        };

        var sessionUpdatedNotification = new SessionUpdatedNotification(notificationAirport, sessionMessage);
        var wrappedNotification = new NotificationContextWrapper<SessionUpdatedNotification>(connectionId, sessionUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));

        var sessionCache = new SessionCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, sessionCache: sessionCache, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe($"Connection {connectionId} attempted to update {notificationAirport} but is connected to {connectionAirport}");
    }

    [Fact]
    public async Task WhenTheClientIsNotTheMaster_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "slave-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";

        var slaveConnection = new Connection(connectionId, partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };

        var sessionMessage = new SessionMessage
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
        };

        var sessionUpdatedNotification = new SessionUpdatedNotification(airportIdentifier, sessionMessage);
        var wrappedNotification = new NotificationContextWrapper<SessionUpdatedNotification>(connectionId, sessionUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));

        var sessionCache = new SessionCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, sessionCache: sessionCache, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe("Only the master can update the sequence");
    }

    [Fact]
    public async Task WhenTheSessionIsUpdated_TheCacheIsUpdatedAndAllOtherClientsAreNotified()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";

        var masterConnection = new Connection(connectionId, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var peer1 = new Connection("peer-1", partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peer2 = new Connection("peer-2", partition, airportIdentifier, "AA_OBS", Role.Observer) { IsMaster = false };
        var peers = new[] { peer1, peer2 };

        var sessionMessage = new SessionMessage
        {
            AirportIdentifier = null,
            PendingFlights = [],
            DeSequencedFlights = [],
            DummyCounter = 42,
            Sequence = new SequenceMessage
            {
                CurrentRunwayMode = null,
                NextRunwayMode = null,
                LastLandingTimeForCurrentMode = default,
                FirstLandingTimeForNextMode = default,
                Flights = [],
                Slots = [],
            }
        };

        var sessionUpdatedNotification = new SessionUpdatedNotification(airportIdentifier, sessionMessage);
        var wrappedNotification = new NotificationContextWrapper<SessionUpdatedNotification>(connectionId, sessionUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(masterConnection)).Returns(peers);

        var sessionCache = new SessionCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, sessionCache: sessionCache, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        var cachedSequence = sessionCache.Get(partition, airportIdentifier);
        cachedSequence.ShouldNotBeNull();
        cachedSequence.ShouldBe(sessionMessage);

        hubProxy.Verify(x => x.Send(
            peer1.Id,
            "SequenceUpdated",
            sessionUpdatedNotification,
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            peer2.Id,
            "SequenceUpdated",
            sessionUpdatedNotification,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    SessionUpdatedNotificationHandler GetHandler(
        IConnectionManager? connectionManager = null,
        SessionCache? sessionCache = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        sessionCache ??= new SessionCache();
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new SessionUpdatedNotificationHandler(connectionManager, sessionCache, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
