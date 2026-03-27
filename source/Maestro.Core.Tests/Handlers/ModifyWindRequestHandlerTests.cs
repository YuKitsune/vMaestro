using Maestro.Contracts.Sessions;
using Maestro.Core.Handlers;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ModifyWindRequestHandlerTests
{
    [Fact]
    public async Task WhenModifyingWind_UpdatesSurfaceWind()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 340, Speed: 25),
            new WindDto(Direction: 320, Speed: 45),
            ManualWind: false);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.Sequence.SurfaceWind.Direction.ShouldBe(340);
        instance.Session.Sequence.SurfaceWind.Speed.ShouldBe(25);
    }

    [Fact]
    public async Task WhenModifyingWind_UpdatesUpperWind()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 340, Speed: 25),
            new WindDto(Direction: 320, Speed: 45),
            ManualWind: false);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.Sequence.UpperWind.Direction.ShouldBe(320);
        instance.Session.Sequence.UpperWind.Speed.ShouldBe(45);
    }

    [Fact]
    public async Task WhenModifyingWind_UpdatesManualWindFlag()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        // Initially false
        instance.Session.Sequence.ManualWind.ShouldBeFalse();

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 340, Speed: 25),
            new WindDto(Direction: 320, Speed: 45),
            ManualWind: true);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.Sequence.ManualWind.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenModifyingWind_PublishesSessionUpdatedNotification()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 340, Speed: 25),
            new WindDto(Direction: 320, Speed: 45),
            ManualWind: true);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        await mediator.Received(1).Publish(
            Arg.Is<SessionUpdatedNotification>(n =>
                n.AirportIdentifier == "YSSY" &&
                n.Session.Sequence.SurfaceWind.Direction == 340 &&
                n.Session.Sequence.SurfaceWind.Speed == 25 &&
                n.Session.Sequence.UpperWind.Direction == 320 &&
                n.Session.Sequence.UpperWind.Speed == 45 &&
                n.Session.Sequence.ManualWind == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenModifyingWind_WithZeroWind_UpdatesToZero()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 0, Speed: 0),
            new WindDto(Direction: 0, Speed: 0),
            ManualWind: false);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.Sequence.SurfaceWind.Direction.ShouldBe(0);
        instance.Session.Sequence.SurfaceWind.Speed.ShouldBe(0);
        instance.Session.Sequence.UpperWind.Direction.ShouldBe(0);
        instance.Session.Sequence.UpperWind.Speed.ShouldBe(0);
    }

    [Fact]
    public async Task WhenModifyingWind_WithDifferentDirectionsAndSpeeds_UpdatesIndependently()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 180, Speed: 10),
            new WindDto(Direction: 270, Speed: 60),
            ManualWind: true);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.Sequence.SurfaceWind.Direction.ShouldBe(180);
        instance.Session.Sequence.SurfaceWind.Speed.ShouldBe(10);
        instance.Session.Sequence.UpperWind.Direction.ShouldBe(270);
        instance.Session.Sequence.UpperWind.Speed.ShouldBe(60);
        instance.Session.Sequence.ManualWind.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenConnectedAsSlave_AndManualUpdate_RelaysRequestToMaster()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 340, Speed: 25),
            new WindDto(Direction: 320, Speed: 45),
            ManualWind: true);

        var originalSurfaceWind = instance.Session.Sequence.SurfaceWind;
        var originalUpperWind = instance.Session.Sequence.UpperWind;
        var originalManualWind = instance.Session.Sequence.ManualWind;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1);
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request);

        // Local session should not be modified when relaying
        instance.Session.Sequence.SurfaceWind.ShouldBe(originalSurfaceWind);
        instance.Session.Sequence.UpperWind.ShouldBe(originalUpperWind);
        instance.Session.Sequence.ManualWind.ShouldBe(originalManualWind);

        // Should not publish notification when relaying
        await mediator.DidNotReceive().Publish(
            Arg.Any<SessionUpdatedNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenConnectedAsSlave_AndAutomaticUpdate_IgnoresRequest()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new ModifyWindRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new ModifyWindRequest(
            "YSSY",
            new WindDto(Direction: 340, Speed: 25),
            new WindDto(Direction: 320, Speed: 45),
            ManualWind: false); // Automatic update

        var originalSurfaceWind = instance.Session.Sequence.SurfaceWind;
        var originalUpperWind = instance.Session.Sequence.UpperWind;
        var originalManualWind = instance.Session.Sequence.ManualWind;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - should not relay automatic updates
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(0);

        // Local session should not be modified
        instance.Session.Sequence.SurfaceWind.ShouldBe(originalSurfaceWind);
        instance.Session.Sequence.UpperWind.ShouldBe(originalUpperWind);
        instance.Session.Sequence.ManualWind.ShouldBe(originalManualWind);

        // Should not publish notification
        await mediator.DidNotReceive().Publish(
            Arg.Any<SessionUpdatedNotification>(),
            Arg.Any<CancellationToken>());
    }
}
