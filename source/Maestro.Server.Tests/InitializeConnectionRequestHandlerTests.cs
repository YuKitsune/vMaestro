using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using Maestro.Server.Handlers;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class InitializeConnectionRequestHandlerTests
{
    const string Version = "0.0.0";

    [Fact]
    public async Task ReturnsConnectionInitializationData()
    {
        // Arrange
        const string connectionId = "connection-1";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";
        const string callsign = "ML-BIK_CTR";
        const Role role = Role.Enroute;

        var connection = new Connection(connectionId, Version, partition, airportIdentifier, callsign, role) { IsMaster = true };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out connection)).Returns(true);
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([connection]);

        var request = new InitializeConnectionRequest();
        var wrappedRequest = new RequestContextWrapper<InitializeConnectionRequest, InitializeConnectionResponse>(connectionId, request);

        // Act
        var response = await GetHandler(connectionManager: connectionManager.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        response.ConnectionId.ShouldBe(connectionId);
        response.Partition.ShouldBe(partition);
        response.AirportIdentifier.ShouldBe(airportIdentifier);
        response.IsMaster.ShouldBeTrue();
        response.ConnectedPeers.ShouldBeEmpty();
        response.Session.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsConnectedPeers()
    {
        // Arrange
        const string connectionId = "connection-2";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";

        var connection1 = new Connection("connection-1", Version, partition, airportIdentifier, "SY_APP", Role.Approach) { IsMaster = true };
        var connection2 = new Connection(connectionId, Version, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute);

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out connection2)).Returns(true);
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([connection1, connection2]);

        var request = new InitializeConnectionRequest();
        var wrappedRequest = new RequestContextWrapper<InitializeConnectionRequest, InitializeConnectionResponse>(connectionId, request);

        // Act
        var response = await GetHandler(connectionManager: connectionManager.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        response.ConnectedPeers.Length.ShouldBe(1);
        response.ConnectedPeers[0].Callsign.ShouldBe("SY_APP");
        response.ConnectedPeers[0].Role.ShouldBe(Role.Approach);
    }

    [Fact]
    public async Task ReturnsCachedSession()
    {
        // Arrange
        const string connectionId = "connection-1";
        const string partition = "partition-1";
        const string airportIdentifier = "YSSY";

        var connection = new Connection(connectionId, Version, partition, airportIdentifier, "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var cachedSession = new SessionMessage
        {
            AirportIdentifier = airportIdentifier,
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

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out connection)).Returns(true);
        connectionManager.Setup(x => x.GetConnections(partition, airportIdentifier)).Returns([connection]);

        var sessionCache = new SessionCache();
        sessionCache.Set(partition, airportIdentifier, cachedSession);

        var request = new InitializeConnectionRequest();
        var wrappedRequest = new RequestContextWrapper<InitializeConnectionRequest, InitializeConnectionResponse>(connectionId, request);

        // Act
        var response = await GetHandler(connectionManager.Object, sessionCache)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        response.Session.ShouldBe(cachedSession);
    }

    [Fact]
    public async Task ThrowsWhenConnectionNotFound()
    {
        // Arrange
        const string connectionId = "connection-1";
        Connection? connection = null;

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out connection)).Returns(false);

        var request = new InitializeConnectionRequest();
        var wrappedRequest = new RequestContextWrapper<InitializeConnectionRequest, InitializeConnectionResponse>(connectionId, request);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await GetHandler(connectionManager: connectionManager.Object)
                .Handle(wrappedRequest, CancellationToken.None));
    }

    InitializeConnectionRequestHandler GetHandler(
        IConnectionManager? connectionManager = null,
        SessionCache? sessionCache = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        sessionCache ??= new SessionCache();
        var logger = new Mock<ILogger>().Object;
        return new InitializeConnectionRequestHandler(connectionManager, sessionCache, logger);
    }
}
