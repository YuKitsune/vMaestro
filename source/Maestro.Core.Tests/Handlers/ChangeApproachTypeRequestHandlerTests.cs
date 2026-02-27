using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ChangeApproachTypeRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenChangingApproachType_TheApproachTypeIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType("A")
            .WithLandingEstimate(now.AddMinutes(32))
            .WithLandingTime(now.AddMinutes(32))
            .WithRunway("34R")
            .WithState(State.Stable) // Make the flight Stable so the ApproachType doesn't get reset when scheduling
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var trajectoryService = Substitute.For<ITrajectoryService>();
        trajectoryService.GetTrajectory(Arg.Any<Flight>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new Trajectory(TimeSpan.FromMinutes(22)));
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", "P");

        flight.ApproachType.ShouldBe("A", "Initial approach type should be A");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ApproachType.ShouldBe("P", "Approach type should be changed to P");
        trajectoryService.Received(1).GetTrajectory(flight, "34R", "P");
    }

    [Fact]
    public async Task WhenChangingApproachType_TheTrajectoryIsUpdated()
    {
        // TODO: @claude, please implement this test case.

        // Arrange
        // TODO: Create a flight
        // TODO: Record it's current trajectory

        // Act
        // TODO: Change the approach type

        // Assert
        // TODO: The new trajectory should not be the same as the original trajectory

        Assert.Fail();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task WhenChangingApproachType_TheLandingEstimateIsUpdated()
    {
        // TODO: @claude Update this test case to ensure the LandingEstimate is FeederFixEstimate + Trajectory.TTG.

        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var feederFixEstimate = now.AddMinutes(10);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(feederFixEstimate)
            .WithApproachType("A")
            .WithLandingEstimate(feederFixEstimate.AddMinutes(22))
            .WithLandingTime(feederFixEstimate.AddMinutes(22))
            .WithRunway("34R")
            .WithState(State.Stable) // Make the flight Stable so the ApproachType doesn't get reset when scheduling
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        // Set up trajectory service to return a different TTG for the new approach type
        var trajectoryService = Substitute.For<ITrajectoryService>();
        trajectoryService.GetTrajectory(Arg.Any<Flight>(), Arg.Any<string>(), "P")
            .Returns(new Trajectory(TimeSpan.FromMinutes(25)));

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", "P");

        flight.ApproachType.ShouldBe("A", "Initial approach type should be A");
        flight.LandingEstimate.ShouldBe(feederFixEstimate.AddMinutes(22), "Initial landing estimate should be FF + 22 minutes (A approach)");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ApproachType.ShouldBe("P", "Approach type should be changed to P");
        flight.LandingEstimate.ShouldBe(feederFixEstimate.AddMinutes(25), "Landing estimate should be updated to FF + 25 minutes (P approach)");
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType("A")
            .WithLandingEstimate(now.AddMinutes(32))
            .WithLandingTime(now.AddMinutes(32))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var trajectoryService = Substitute.For<ITrajectoryService>();
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            instanceManager,
            slaveConnectionManager,
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", "P");

        var originalApproachType = flight.ApproachType;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        flight.ApproachType.ShouldBe(originalApproachType, "Local flight should not be modified when relaying to master");
    }
}
