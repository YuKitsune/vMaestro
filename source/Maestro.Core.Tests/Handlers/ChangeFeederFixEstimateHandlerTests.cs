using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Shouldly;
using Serilog;

namespace Maestro.Core.Tests.Handlers;

public class ChangeFeederFixEstimateHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenChangingFeederFixEstimate_ManualFeederFixShouldBeTrue()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var newEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newEstimate);
        var handler = GetHandler(instanceManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ManualFeederFixEstimate.ShouldBeTrue();
        flight.FeederFixEstimate.ShouldBe(newEstimate);
    }

    [Fact]
    public async Task WhenChangingFeederFixEstimate_LandingEstimateShouldBeRecalculated()
    {
        // Arrange
        var timeToGo = TimeSpan.FromMinutes(10);
        var trajectory = new Trajectory(timeToGo);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithTrajectory(trajectory)
            .Build();

        var trajectoryService = new MockTrajectoryService(timeToGo);

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var expectedLandingEstimate = newFeederFixEstimate.Add(timeToGo);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(instanceManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingEstimate.ShouldBe(expectedLandingEstimate);
    }

    [Fact]
    public async Task WhenChangingFeederFixEstimate_SequenceIsRecalculated()
    {
        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(15))
            .WithLandingEstimate(clockFixture.Instance.UtcNow().AddMinutes(25))
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight2, flight1)) // QFA2 first, QFA1 second
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1);
        sequence.NumberInSequence(flight1).ShouldBe(2);

        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(5);
        var timeToGo = TimeSpan.FromMinutes(10);
        var newLandingEstimate = newFeederFixEstimate.Add(timeToGo);

        // Set up a trajectory on the flight via SetRunway
        var trajectory = new Trajectory(timeToGo);
        flight1.SetRunway(flight1.AssignedRunwayIdentifier ?? "34L", trajectory);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(instanceManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - QFA1 should now be first due to earlier landing estimate
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA1 should be first after feeder fix estimate change");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA2 should be second after QFA1's estimate change");
    }

    [Fact]
    public async Task WhenFlightIsUnstable_ItShouldBeMadeStable()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(instanceManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task WhenFlightIsNotUnstable_StateIsRetained(State state)
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(instanceManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        // Arrange
        var originalFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixEstimate)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // Create a mock connection manager that simulates a slave connection
        var mockConnectionManager = new MockSlaveConnectionManager();

        var newEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newEstimate);
        var handler = new ChangeFeederFixEstimateRequestHandler(
            instanceManager,
            mockConnectionManager,
            clockFixture.Instance,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Verify the request was relayed to the master
        mockConnectionManager.Connection.InvokedRequests.ShouldContain(request, "request should have been relayed to master");

        // Verify the flight was NOT modified locally (proving redirection occurred)
        flight.ManualFeederFixEstimate.ShouldBeFalse("flight should not have been modified locally");
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate, "feeder fix estimate should remain unchanged");
    }

    ChangeFeederFixEstimateRequestHandler GetHandler(
        IMaestroInstanceManager instanceManager,
        IMediator? mediator = null,
        ILogger? logger = null)
    {
        mediator ??= Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();

        return new ChangeFeederFixEstimateRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            clockFixture.Instance,
            mediator,
            logger);
    }
}
