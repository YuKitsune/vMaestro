using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using MediatR;
using Moq;
using Shouldly;
using ILogger = Serilog.ILogger;

namespace Maestro.Server.Tests;

public class RelayToMasterRequestHandlerTests
{
    [Fact]
    public async Task WhenTheConnectionIsNotTracked_ExceptionIsThrown()
    {
        // Arrange
        const string connectionId = "unknown-connection";
        var testRequest = new TestRequest("test-data");
        var relayRequest = new RelayToMasterRequest("TestMethod", testRequest);
        var wrappedRequest = new RequestContextWrapper<RelayToMasterRequest, RelayResponse>(connectionId, relayRequest);

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
    public async Task WhenTheConnectionIsTheMaster_FailureResponseIsReturned()
    {
        // Arrange
        const string connectionId = "master-connection";
        var testRequest = new TestRequest("test-data");
        var relayRequest = new RelayToMasterRequest("TestMethod", testRequest);
        var wrappedRequest = new RequestContextWrapper<RelayToMasterRequest, RelayResponse>(connectionId, relayRequest);

        var masterConnection = new Connection(connectionId, "partition-1", "YSSY", "ML-BIK_CTR", Role.Enroute) { IsMaster = true };

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = masterConnection;
                return true;
            }));

        var hubProxy = new Mock<IHubProxy>();
        var logger = new Mock<ILogger>();

        // Act
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object, logger: logger.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Cannot relay to self");

        logger.Verify(x => x.Warning("{Connection} attempted to relay to itself", masterConnection), Times.Once);
    }

    [Fact]
    public async Task WhenNoMasterFound_FailureResponseIsReturned()
    {
        // Arrange
        const string connectionId = "slave-connection";
        var testRequest = new TestRequest("test-data");
        var relayRequest = new RelayToMasterRequest("TestMethod", testRequest);
        var wrappedRequest = new RequestContextWrapper<RelayToMasterRequest, RelayResponse>(connectionId, relayRequest);

        var slaveConnection = new Connection(connectionId, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false };
        var peerConnections = new[]
        {
            new Connection("peer-1", "partition-1", "YSSY", "ML-BIK_CTR", Role.Enroute) { IsMaster = false },
            new Connection("peer-2", "partition-1", "YSSY", "AA_OBS", Role.Observer) { IsMaster = false }
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
        var logger = new Mock<ILogger>();

        // Act
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object, logger: logger.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("No master found");

        logger.Verify(x => x.Error("No master found"), Times.Once);
    }

    [Fact]
    public async Task RequestsAreRelayedToTheMasterInAnEnvelope()
    {
        // Arrange
        const string connectionId = "slave-connection";
        const string masterConnectionId = "master-connection";
        var testRequest = new TestRequest("test-data");
        var relayRequest = new RelayToMasterRequest("TestMethod", testRequest);
        var wrappedRequest = new RequestContextWrapper<RelayToMasterRequest, RelayResponse>(connectionId, relayRequest);

        var slaveConnection = new Connection(connectionId, "partition-1", "YSSY", "SY_APP", Role.Approach) { IsMaster = false };
        var masterConnection = new Connection(masterConnectionId, "partition-1", "YSSY", "ML-BIK_CTR", Role.Enroute) { IsMaster = true };
        var peerConnections = new[] { masterConnection };

        var expectedResponse = RelayResponse.CreateSuccess();

        var connectionManager = new Mock<IConnectionManager>();
        connectionManager.Setup(x => x.TryGetConnection(connectionId, out It.Ref<Connection?>.IsAny))
            .Returns(new TryGetConnectionCallback((string id, out Connection? connection) =>
            {
                connection = slaveConnection;
                return true;
            }));
        connectionManager.Setup(x => x.GetPeers(slaveConnection)).Returns(peerConnections);

        var hubProxy = new Mock<IHubProxy>();
        hubProxy.Setup(x => x.Invoke<RequestEnvelope, RelayResponse>(
                masterConnectionId,
                "TestMethod",
                It.IsAny<RequestEnvelope>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await GetHandler(connectionManager: connectionManager.Object, hubProxy: hubProxy.Object)
            .Handle(wrappedRequest, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResponse);

        hubProxy.Verify(x => x.Invoke<RequestEnvelope, RelayResponse>(
            masterConnectionId,
            "TestMethod",
            It.Is<RequestEnvelope>(envelope =>
                envelope.OriginatingCallsign == "SY_APP" &&
                envelope.OriginatingConnectionId == connectionId &&
                envelope.OriginatingRole == Role.Approach &&
                envelope.Request == testRequest),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    RelayToMasterRequestHandler GetHandler(
        IConnectionManager? connectionManager = null,
        IHubProxy? hubProxy = null,
        ILogger? logger = null)
    {
        connectionManager ??= new Mock<IConnectionManager>().Object;
        hubProxy ??= new Mock<IHubProxy>().Object;
        logger ??= new Mock<ILogger>().Object;
        return new RelayToMasterRequestHandler(connectionManager, hubProxy, logger);
    }

    public record TestRequest(string Data) : IRequest;

    delegate bool TryGetConnectionCallback(string connectionId, out Connection? connection);
}
