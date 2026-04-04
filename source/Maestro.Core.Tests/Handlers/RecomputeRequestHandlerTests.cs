using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class RecomputeRequestHandlerTests(ClockFixture clockFixture)
{
    static readonly TimeSpan AcceptanceRate = TimeSpan.FromSeconds(180);
    readonly TimeSpan _defaultTtg = TimeSpan.FromMinutes(20);

    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways(DefaultRunway)
            .WithFeederFixes("RIVET", "WELSH")
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = DefaultRunway,
                LandingRateSeconds = DefaultLandingRateSeconds,
                FeederFixes = ["RIVET", "WELSH"]
            })
            .WithTrajectory("RIVET", DefaultRunway, 15)
            .WithTrajectory("WELSH", DefaultRunway, 17)
            .Build();
    }

    readonly RunwayMode _runwayMode = new(
        new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            Runways =
            [
                new RunwayConfiguration { Identifier = "34L", ApproachType = string.Empty, LandingRateSeconds = 180, FeederFixes = ["RIVET", "WELSH"]},
                new RunwayConfiguration { Identifier = "34R", ApproachType = string.Empty, LandingRateSeconds = 180, FeederFixes = ["RIVET", "WELSH"] }
            ]
        });

    [Fact]
    public async Task TheFlightIsMovedBasedOnItsLandingEstimate()
    {
        var now = clockFixture.Instance.UtcNow();
        var ttg = TimeSpan.FromMinutes(10);

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithTrajectory(new Trajectory(ttg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(30))
            .WithTrajectory(new Trajectory(ttg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, session, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithRunwayMode(_runwayMode)
                .WithFlightsInOrder(flight1, flight2))
            .Build();

        sequence.Flights[0].Callsign.ShouldBe("QFA1");
        sequence.Flights[1].Callsign.ShouldBe("QFA2");

        var handler = GetRequestHandler(sessionManager, sequence, trajectoryService);

        // Update flight2's estimates so its landing estimate is earlier than flight1
        session.FlightDataRecords["QFA2"] = new FlightDataRecord(
            "QFA2", flight2.AircraftType, flight2.AircraftCategory, flight2.WakeCategory,
            flight2.OriginIdentifier, flight2.DestinationIdentifier, null, null,
            [new FixEstimate("RIVET", now.AddMinutes(5)), new FixEstimate("YSSY", now.AddMinutes(15))],
            now);

        var request = new RecomputeRequest("YSSY", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights[0].Callsign.ShouldBe("QFA2", "flight2 should be first (earlier landing estimate)");
        sequence.Flights[1].Callsign.ShouldBe("QFA1", "flight1 should be second (later landing estimate)");
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var trajectoryService = new MockTrajectoryService(_defaultTtg);

        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(-10))
            .WithTrajectory(new Trajectory(_defaultTtg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(-5))
            .WithTrajectory(new Trajectory(_defaultTtg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(0))
            .WithTrajectory(new Trajectory(_defaultTtg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithSingleRunway("34L", TimeSpan.FromSeconds(180))
                .WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence, trajectoryService);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Change the landing estimate of the last flight to be earlier than the second flight
        var newFeederFixEstimate = now.AddMinutes(-8);
        session.FlightDataRecords["QFA3"] = new FlightDataRecord(
            "QFA3", flight3.AircraftType, flight3.AircraftCategory, flight3.WakeCategory,
            flight3.OriginIdentifier, flight3.DestinationIdentifier, null, null,
            [new FixEstimate("RIVET", newFeederFixEstimate), new FixEstimate("YSSY", newFeederFixEstimate.Add(_defaultTtg))],
            now);

        var request = new RecomputeRequest("YSSY", "QFA3");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Order by FeederFixEstimate: QFA1 (-10), QFA3 (-8), QFA2 (-5)
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1 (FF=-10)");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "Second flight should be QFA3 (FF=-8, moved forward)");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "Third flight should be QFA2 (FF=-5)");

        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "First flight's landing time should be unchanged");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "First flight should land at its estimate");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(AcceptanceRate), "Flight 3 should be behind flight 1 separated by acceptance rate");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(AcceptanceRate), "Flight 2 should be behind flight 3 separated by acceptance rate");
    }

    [Fact]
    public async Task ManualFeederFixEstimateIsRemoved()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(5), manual: true)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        flight.ManualFeederFixEstimate.ShouldBeTrue("Manual feeder fix estimate should initially be set");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ManualFeederFixEstimate.ShouldBeFalse("Manual feeder fix estimate should be removed after recompute");
    }

    [Fact]
    public async Task FeederFixIsReCalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var landingTime = now.AddMinutes(20);
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(5))
            .WithLandingTime(landingTime)
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act

        // Re-route the flight to a new feeder fix
        var newFeederFixEstimate = now.AddMinutes(3);
        session.FlightDataRecords["QFA1"] = new FlightDataRecord(
            "QFA1", flight.AircraftType, flight.AircraftCategory, flight.WakeCategory,
            flight.OriginIdentifier, flight.DestinationIdentifier, null, null,
            [new FixEstimate("WELSH", newFeederFixEstimate), new FixEstimate("TESAT", landingTime)],
            now);

        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.FeederFixIdentifier.ShouldBe("WELSH");
        flight.FeederFixEstimate.ShouldBe(newFeederFixEstimate);
    }

    [Fact]
    public async Task RunwayIsReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(now.AddMinutes(20))
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34L"); // Default runway from runway mode
    }

    [Fact]
    public async Task MaximumDelayIsReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(now.AddMinutes(20))
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .ManualDelay(TimeSpan.FromMinutes(5))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.MaximumDelay.ShouldBeNull();
    }

    [Fact]
    public async Task TargetLandingTimeIsReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(8))
            .WithLandingTime(now.AddMinutes(10))
            .WithTargetLandingTime(now.AddMinutes(10))
            .ManualDelay(TimeSpan.FromMinutes(5))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.TargetLandingTime.ShouldBeNull();
        flight.LandingTime.ShouldBe(flight.LandingEstimate);
    }

    [Fact]
    public async Task WhenRecomputingAFlight_SequenceUpdatedNotificationIsPublished()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var trajectoryService = new MockTrajectoryService(_defaultTtg);

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(-10))
            .WithTrajectory(new Trajectory(_defaultTtg))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var handler = GetRequestHandler(sessionManager, sequence, trajectoryService, mediator);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        await mediator.Received(1).Publish(
            Arg.Is<SessionUpdatedNotification>(n => n.AirportIdentifier == "YSSY"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenRecomputingAFlight_StateIsUpdatedBasedOnPositionInSequence(State expectedState)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        DateTimeOffset feederFixEstimate;
        var ttg = TimeSpan.FromMinutes(30);

        // Configure times based on expected state
        switch (expectedState)
        {
            case State.Unstable:
                feederFixEstimate = now.AddMinutes(35); // More than 25 minutes to feeder fix
                // Landing = now + 35 + 30 = now + 65
                break;
            case State.Stable:
                feederFixEstimate = now.AddMinutes(20); // Within 25 minutes of feeder fix
                // Landing = now + 20 + 30 = now + 50
                break;
            case State.SuperStable:
                feederFixEstimate = now.AddMinutes(-5); // Past initial feeder fix time
                // Landing = now - 5 + 30 = now + 25
                break;
            case State.Frozen:
                feederFixEstimate = now.AddMinutes(-10); // Landing within 15 minutes
                ttg = TimeSpan.FromMinutes(20); // Landing = now - 10 + 20 = now + 10
                break;
            case State.Landed:
                feederFixEstimate = now.AddMinutes(-25); // Past scheduled landing time
                ttg = TimeSpan.FromMinutes(20); // Landing = now - 25 + 20 = now - 5 (in the past)
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedState));
        }

        var trajectoryService = new MockTrajectoryService(ttg);

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(feederFixEstimate)
            .WithTrajectory(new Trajectory(ttg))
            .WithActivationTime(now.Subtract(TimeSpan.FromMinutes(10)))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence, trajectoryService);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(expectedState);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var airportConfiguration = CreateAirportConfiguration();
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);

        var trajectoryService = new MockTrajectoryService();
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<Serilog.ILogger>();

        var handler = new RecomputeRequestHandler(
            sessionManager,
            slaveConnectionManager,
            airportConfigurationProvider,
            trajectoryService,
            clockFixture.Instance,
            mediator,
            logger);

        var request = new RecomputeRequest("YSSY", "QFA1");

        var originalFlightLandingTime = flight.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        flight.LandingTime.ShouldBe(originalFlightLandingTime, "Flight should not be modified locally when relaying to master");
    }

    RecomputeRequestHandler GetRequestHandler(
        ISessionManager sessionManager,
        Sequence sequence,
        ITrajectoryService? trajectoryService = null,
        IMediator? mediator = null)
    {
        var airportConfiguration = CreateAirportConfiguration();
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);

        trajectoryService ??= new MockTrajectoryService();
        mediator ??= Substitute.For<IMediator>();

        var logger = Substitute.For<Serilog.ILogger>();

        return new RecomputeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            trajectoryService,
            clockFixture.Instance,
            mediator,
            logger);
    }
}
