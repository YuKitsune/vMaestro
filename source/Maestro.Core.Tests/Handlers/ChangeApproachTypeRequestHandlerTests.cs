using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ChangeApproachTypeRequestHandlerTests(ClockFixture clockFixture)
{
    const string FirstApproachType = "A";
    const int FirstApproachTypeTtg = 20;

    const string SecondApproachType = "P";
    const int SecondApproachTypeTtg = 25;

    [Fact]
    public async Task WhenChangingApproachType_TheApproachTypeIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType(FirstApproachType)
            .WithLandingEstimate(now.AddMinutes(32))
            .WithLandingTime(now.AddMinutes(32))
            .WithRunway("34R")
            .WithState(State.Stable) // Make the flight Stable so the ApproachType doesn't get reset when scheduling
            .Build();

        // Configure sequence builder with trajectory service to ensure initial landing estimate is correct
        var sequenceTrajectoryService = new MockTrajectoryService(TimeSpan.FromMinutes(22));
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(sequenceTrajectoryService).WithFlightsInOrder(flight))
            .Build();

        var trajectoryService = new TrajectoryService(
            new AirportConfigurationProvider([airportConfiguration]),
            Substitute.For<IPerformanceLookup>(),
            Substitute.For<ILogger>());

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", SecondApproachType);

        flight.ApproachType.ShouldBe(FirstApproachType, $"Initial approach type should be {FirstApproachType}");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ApproachType.ShouldBe(SecondApproachType, $"Approach type should be changed to {SecondApproachType}");
        flight.TerminalTrajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(SecondApproachTypeTtg), "Changing approach type should update the trajectory");
    }

    [Fact]
    public async Task WhenChangingApproachType_TheTrajectoryIsUpdated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType(FirstApproachType)
            .WithTrajectory(new TerminalTrajectory(TimeSpan.FromMinutes(FirstApproachTypeTtg), default, default))
            .WithRunway("34R")
            .WithState(State.Stable) // Make the flight Stable so the ApproachType doesn't get reset when scheduling
            .Build();

        // Set up trajectory service to return different TTG for each approach type
        var trajectoryService = new TrajectoryService(
            new AirportConfigurationProvider([airportConfiguration]),
            Substitute.For<IPerformanceLookup>(),
            Substitute.For<ILogger>());

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", SecondApproachType);

        // Record the current trajectory
        var originalTrajectory = flight.TerminalTrajectory;
        originalTrajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(FirstApproachTypeTtg), $"Initial trajectory should be {FirstApproachTypeTtg} minutes");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.TerminalTrajectory.ShouldNotBe(originalTrajectory, "Trajectory should be updated");
        flight.TerminalTrajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(SecondApproachTypeTtg), $"Trajectory should be updated to {SecondApproachTypeTtg} minutes for approach {SecondApproachType}");
    }

    [Fact]
    public async Task WhenChangingApproachType_TheLandingEstimateIsUpdated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var feederFixEstimate = now.AddMinutes(10);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(feederFixEstimate)
            .WithApproachType(FirstApproachType)
            .WithLandingEstimate(feederFixEstimate.AddMinutes(FirstApproachTypeTtg))
            .WithLandingTime(feederFixEstimate.AddMinutes(FirstApproachTypeTtg))
            .WithRunway("34R")
            .WithState(State.Stable) // Make the flight Stable so the ApproachType doesn't get reset when scheduling
            .Build();

        // Set up trajectory service to return different TTG for each approach type
        var trajectoryService = new TrajectoryService(
            new AirportConfigurationProvider([airportConfiguration]),
            Substitute.For<IPerformanceLookup>(),
            Substitute.For<ILogger>());

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", SecondApproachType);

        flight.ApproachType.ShouldBe(FirstApproachType, $"Initial approach type should be {FirstApproachType}");
        flight.LandingEstimate.ShouldBe(feederFixEstimate.AddMinutes(FirstApproachTypeTtg), $"Initial landing estimate should be FF + {FirstApproachTypeTtg} minutes ({FirstApproachType} approach)");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ApproachType.ShouldBe(SecondApproachType, $"Approach type should be changed to {SecondApproachType}");
        flight.LandingEstimate.ShouldBe(feederFixEstimate.AddMinutes(SecondApproachTypeTtg), $"Landing estimate should be updated to FF + {SecondApproachTypeTtg} minutes ({SecondApproachType} approach)");
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType(FirstApproachType)
            .WithLandingEstimate(now.AddMinutes(32))
            .WithLandingTime(now.AddMinutes(32))
            .WithRunway("34R")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var trajectoryService = new MockTrajectoryService();
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            sessionManager,
            slaveConnectionManager,
            new AirportConfigurationProvider([airportConfiguration]),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", SecondApproachType);

        var originalApproachType = flight.ApproachType;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        flight.ApproachType.ShouldBe(originalApproachType, "Local flight should not be modified when relaying to master");
    }

    [Fact]
    public async Task WhenChangingApproachType_AndTheFlightWasUnstable_ItBecomesStable()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType(FirstApproachType)
            .WithLandingEstimate(now.AddMinutes(30))
            .WithLandingTime(now.AddMinutes(30))
            .WithRunway("34R")
            .WithState(State.Unstable)
            .Build();

        var trajectoryService = new MockTrajectoryService();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", SecondApproachType);

        flight.State.ShouldBe(State.Unstable, "Flight should initially be Unstable");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable, "Unstable flight should become Stable when approach type is changed");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task WhenChangingApproachType_AndTheFlightWasNotUnstable_StateRemainsUnchanged(State state)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType(FirstApproachType)
            .WithLandingEstimate(now.AddMinutes(30))
            .WithLandingTime(now.AddMinutes(30))
            .WithRunway("34R")
            .WithState(state)
            .Build();

        var trajectoryService = new MockTrajectoryService();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeApproachTypeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", SecondApproachType);

        flight.State.ShouldBe(state, $"Flight should initially be {state}");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state, $"Flight state should remain {state} when changing approach type");
    }

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways("34R")
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = "34R",
                LandingRateSeconds = 180,
                FeederFixes = []
            })
            .WithTrajectory("BOREE", [new AllAircraftTypesDescriptor()], FirstApproachType, "34R", FirstApproachTypeTtg)
            .WithTrajectory("BOREE", [new AllAircraftTypesDescriptor()], SecondApproachType, "34R", SecondApproachTypeTtg)
            .Build();
    }
}
