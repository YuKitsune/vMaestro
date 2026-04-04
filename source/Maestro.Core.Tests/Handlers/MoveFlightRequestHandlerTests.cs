using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Handlers;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class MoveFlightRequestHandlerTests(ClockFixture clockFixture)
{
    static readonly TimeSpan AcceptanceRate = TimeSpan.FromSeconds(180);

    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    [Fact]
    public async Task TargetLandingTimeIsSet()
    {
        var now = clockFixture.Instance.UtcNow();
        var landingEstimate = now.AddMinutes(10);
        var originalLandingTime = now.AddMinutes(10);

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithLandingTime(originalLandingTime)
            .WithRunway("34L")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            "34L",
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingEstimate.ShouldBe(landingEstimate, "Moving a flight should not affect the landing estimate");
        flight.TargetLandingTime.ShouldBe(newLandingTime, "Moving a flight should set the TargetLandingTime");
        flight.LandingTime.ShouldBe(newLandingTime, "Moving a flight should schedule the it at the TargetLandingTime");
    }

    [Fact]
    public async Task RunwayIsSet()
    {
        var now = clockFixture.Instance.UtcNow();
        var landingEstimate = now.AddMinutes(10);
        var originalLandingTime = now.AddMinutes(10);

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithLandingTime(originalLandingTime)
            .WithRunway("34L")
            .Build();

        var airportConfig = CreateDualRunwayConfiguration();
        var (sessionManager, _, _) = new SessionBuilder(airportConfig)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfig);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            "34R",
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R", "Moving a flight should assign the desired runway");
    }

    [Fact]
    public async Task ApproachTypeRemainsUnchangedWhenMovingRunways()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        // Create configuration with 34L using A and 34R using P in mode
        var airportConfig = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithRunwayMode(new RunwayModeConfiguration
            {
                Identifier = "34IVA",
                DependencyRateSeconds = 0,
                OffModeSeparationSeconds = 0,
                Runways =
                [
                    new RunwayConfiguration
                    {
                        Identifier = "34L",
                        ApproachType = "A",
                        LandingRateSeconds = DefaultLandingRateSeconds,
                        FeederFixes = []
                    },
                    new RunwayConfiguration
                    {
                        Identifier = "34R",
                        ApproachType = "P",
                        LandingRateSeconds = DefaultLandingRateSeconds,
                        FeederFixes = []
                    }
                ]
            })
            .Build();

        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfig)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfig);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", "34R", newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R", "Flight should be assigned to the requested runway");
        flight.ApproachType.ShouldBe("A", "Flight should retain its original approach type when moved to a different runway");
    }

    [Fact]
    public async Task TrajectoryRecalculatedWhenMovingRunways()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfig = CreateDualRunwayConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(20)))
            .Build();

        var trajectoryFor34R = new Trajectory(TimeSpan.FromMinutes(25));
        var trajectoryService = new MockTrajectoryService()
            .WithTrajectory()
            .OnRunway("34R")
            .WithApproach("A")
            .Returns(trajectoryFor34R);

        var (sessionManager, _, _) = new SessionBuilder(airportConfig)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfig, trajectoryService: trajectoryService);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", "34R", newLandingTime);

        var originalTrajectory = flight.Trajectory;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R", "Flight should be assigned to the requested runway");
        flight.ApproachType.ShouldBe("A", "Approach type should remain unchanged");
        flight.Trajectory.ShouldBe(trajectoryFor34R, "Trajectory should be recalculated for the new runway");
        flight.Trajectory.ShouldNotBe(originalTrajectory, "Trajectory should be different from the original");
    }

    [Fact]
    public async Task UnstableFlightsBecomeStable()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        flight.State.ShouldBe(State.Unstable, "Flight should initially be Unstable");

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", "34L", newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable, "Unstable flight should become Stable after being moved");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task StableFlightsDoNotChangeState(State state)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        flight.State.ShouldBe(state, $"Flight should initially be {state}");

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", "34L", newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state, $"Flight state should remain {state} after being moved");
    }

    [Fact]
    public async Task FlightIsPositionedBasedOnTargetTime()
    {
        // TODO: @claude, ensure this scenario does not position flights based on FeederFixTimes.
        //  Vary the TTG for the other flights, such that if they were positioned by FeederFixTime, the landing order would be different.

        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", "34L", newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should remain in it's current position");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "The moved flight should be moved forward one position");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "The original flight in position 2 should move behind the moved flight");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task MovedFlightIsDelayedBehindLeadingFlight(State leadingFlightState)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(leadingFlightState)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        // Move QFA2 to _just_ behind QFA1
        var newLandingTime = now.AddMinutes(11);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            "34L",
            newLandingTime);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Leading flight should remain unchanged");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(AcceptanceRate), "Moved flight should be moved forward to the target time, then delayed for separation with the leading flight");
        flight3.LandingTime.ShouldBe(flight3.LandingEstimate, "Third flight should be unaffected");
    }

    [Fact]
    public async Task TrailingFlightsAreDelayedBehindMovedFlight()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        // Move QFA2 to _just_ in front of QFA3
        var newLandingTime = now.AddMinutes(19);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            "34L",
            newLandingTime);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "First flight should remain unchanged");
        flight2.LandingTime.ShouldBe(newLandingTime, "Moved flight should be moved back to the target time");
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(AcceptanceRate), "Third flight should be delayed behind the moved flight");
    }

    [Fact]
    public async Task CannotMoveInFrontOfFrozenFlightWithoutEnoughSpace()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        var newLandingTime = now.AddMinutes(9);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            "34L",
            newLandingTime);

        // Act & Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task CanMoveInFrontOfFrozenFlightWithEnoughSpace()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        var newLandingTime = now.AddMinutes(5);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            "34L",
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.LandingTime.ShouldBe(newLandingTime, "Moved flight should move to the target time");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "Frozen flight should be unaffected");
    }

    [Fact]
    public async Task CannotMoveBetweenFrozenFlightsWithoutEnoughSpace()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(18))
            .WithLandingTime(now.AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        var newLandingTime = now.AddMinutes(13);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            "34L",
            newLandingTime);

        // Act & Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task CanMoveBetweenFrozenFlightsWithEnoughSpace()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(sessionManager);

        // Move the flight perfectly between the two frozen flights
        var newLandingTime = now.AddMinutes(13);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            "34L",
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "First frozen flight should remain unchanged");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(AcceptanceRate), "Moved flight should be sequenced between the two frozen flight");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(AcceptanceRate), "Second frozen flight should remain unchanged");
    }

    [Fact]
    public async Task RedirectsToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = GetRequestHandler(sessionManager, airportConfiguration, slaveConnectionManager, mediator);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", "34L", newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "Sequence should not be modified locally when relaying to master");
        sequence.Flights[1].Callsign.ShouldBe("QFA2", "Sequence should not be modified locally when relaying to master");
        sequence.Flights[2].Callsign.ShouldBe("QFA3", "Sequence should not be modified locally when relaying to master");
    }

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways(DefaultRunway)
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = DefaultRunway,
                LandingRateSeconds = DefaultLandingRateSeconds,
                FeederFixes = []
            })
            .Build();
    }

    static AirportConfiguration CreateDualRunwayConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithRunwayMode(new RunwayModeConfiguration
            {
                Identifier = "34IVA",
                DependencyRateSeconds = 0,
                OffModeSeparationSeconds = 0,
                Runways =
                [
                    new RunwayConfiguration
                    {
                        Identifier = "34L",
                        ApproachType = "A",
                        LandingRateSeconds = DefaultLandingRateSeconds,
                        FeederFixes = []
                    },
                    new RunwayConfiguration
                    {
                        Identifier = "34R",
                        ApproachType = "P",
                        LandingRateSeconds = DefaultLandingRateSeconds,
                        FeederFixes = []
                    }
                ]
            })
            .Build();
    }

    MoveFlightRequestHandler GetRequestHandler(
        ISessionManager sessionManager,
        AirportConfiguration? airportConfiguration = null,
        IMaestroConnectionManager? connectionManager = null,
        IMediator? mediator = null,
        MockTrajectoryService? trajectoryService = null,
        IPerformanceLookup? performanceLookup = null)
    {
        airportConfiguration ??= CreateAirportConfiguration();
        var configProvider = new AirportConfigurationProvider([airportConfiguration]);
        trajectoryService ??= new MockTrajectoryService();
        performanceLookup ??= Substitute.For<IPerformanceLookup>();
        mediator ??= Substitute.For<IMediator>();
        var clock = clockFixture.Instance;
        return new MoveFlightRequestHandler(
            sessionManager,
            connectionManager ?? new MockLocalConnectionManager(),
            configProvider,
            trajectoryService,
            performanceLookup,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
