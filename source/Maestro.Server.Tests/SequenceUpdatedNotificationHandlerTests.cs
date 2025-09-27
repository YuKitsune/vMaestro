using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class SequenceUpdatedNotificationHandlerTests
{
    [Fact]
    public async Task WhenTheClientIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        const string airportIdentifier = "YSSY";

        var sequenceMessage = new SequenceMessage
        {
            AirportIdentifier = airportIdentifier,
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

        var sequenceUpdatedNotification = new SequenceUpdatedNotification(airportIdentifier, sequenceMessage);
        var wrappedNotification = new NotificationContextWrapper<SequenceUpdatedNotification>(connectionId, sequenceUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny)).Returns(false);

        var sequenceCache = new SequenceCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, sequenceCache: sequenceCache, hubProxy: hubProxy.Object)
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

        var sequenceMessage = new SequenceMessage
        {
            AirportIdentifier = notificationAirport,
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

        var sequenceUpdatedNotification = new SequenceUpdatedNotification(notificationAirport, sequenceMessage);
        var wrappedNotification = new NotificationContextWrapper<SequenceUpdatedNotification>(connectionId, sequenceUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));

        var sequenceCache = new SequenceCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, sequenceCache: sequenceCache, hubProxy: hubProxy.Object)
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

        var sequenceMessage = new SequenceMessage
        {
            AirportIdentifier = airportIdentifier,
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

        var sequenceUpdatedNotification = new SequenceUpdatedNotification(airportIdentifier, sequenceMessage);
        var wrappedNotification = new NotificationContextWrapper<SequenceUpdatedNotification>(connectionId, sequenceUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));

        var sequenceCache = new SequenceCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await GetHandler(connectionManager: connectionManager.Object, sequenceCache: sequenceCache, hubProxy: hubProxy.Object)
                .Handle(wrappedNotification, CancellationToken.None));

        exception.Message.ShouldBe("Only the master can update the sequence");
    }

    [Fact]
    public async Task WhenTheSequenceIsUpdated_TheCacheIsUpdatedAndAllOtherClientsAreNotified()
    {
        // Arrange
        const string connectionId = "master-connection";
        const string airportIdentifier = "YSSY";
        const string partition = "partition-1";

        var masterConnection = new Connection(connectionId, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var peer1 = new Connection("peer-1", partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = false };
        var peer2 = new Connection("peer-2", partition, airportIdentifier, "AA_OBS", Role.Observer) { IsMaster = false };
        var peers = new[] { peer1, peer2 };

        var sequenceMessage = new SequenceMessage
        {
            AirportIdentifier = airportIdentifier,
            CurrentRunwayMode = null,
            NextRunwayMode = null,
            LastLandingTimeForCurrentMode = default,
            FirstLandingTimeForNextMode = default,
            Flights = [],
            PendingFlights = [],
            DeSequencedFlights = [],
            Slots = [],
            DummyCounter = 42
        };

        var sequenceUpdatedNotification = new SequenceUpdatedNotification(airportIdentifier, sequenceMessage);
        var wrappedNotification = new NotificationContextWrapper<SequenceUpdatedNotification>(connectionId, sequenceUpdatedNotification);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(masterConnection)).Returns(peers);

        var sequenceCache = new SequenceCache();
        var hubProxy = new Mock<IHubProxy>();

        // Act
        await GetHandler(connectionManager: connectionManager.Object, sequenceCache: sequenceCache, hubProxy: hubProxy.Object)
            .Handle(wrappedNotification, CancellationToken.None);

        // Assert
        var cachedSequence = sequenceCache.Get(partition, airportIdentifier);
        cachedSequence.ShouldNotBeNull();
        cachedSequence.ShouldBe(sequenceMessage);

        hubProxy.Verify(x => x.Send(
            peer1.Id,
            "SequenceUpdated",
            sequenceUpdatedNotification,
            It.IsAny<CancellationToken>()), Times.Once);

        hubProxy.Verify(x => x.Send(
            peer2.Id,
            "SequenceUpdated",
            sequenceUpdatedNotification,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    SequenceUpdatedNotificationHandler GetHandler(
        IConnectionManager? connectionManager = null,
        SequenceCache? sequenceCache = null,
        IHubProxy? hubProxy = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        sequenceCache ??= new SequenceCache();
        hubProxy ??= new Mock<IHubProxy>().Object;
        var logger = new Mock<ILogger>().Object;
        return new SequenceUpdatedNotificationHandler(connectionManager, sequenceCache, hubProxy, logger);
    }

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
