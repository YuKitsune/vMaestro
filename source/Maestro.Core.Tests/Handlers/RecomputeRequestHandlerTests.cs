using Maestro.Core.Configuration;
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

namespace Maestro.Core.Tests.Handlers;

public class RecomputeRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;
    readonly TimeSpan _defaultTtg = TimeSpan.FromMinutes(20);

    readonly RunwayMode _runwayMode = new(
        new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            Runways =
            [
                new RunwayConfiguration { Identifier = "34L", ApproachType = string.Empty, LandingRateSeconds = 180, FeederFixes = []},
                new RunwayConfiguration { Identifier = "34R", ApproachType = string.Empty, LandingRateSeconds = 180, FeederFixes = [] }
            ]
        });

    [Fact]
    public async Task TheFlightIsMovedBasedOnItsFeederFixEstimate()
    {
        // Use different TTG values to prove positioning is based on FeederFixEstimate, not LandingEstimate
        // flight1: FF=+10, TTG=5, Landing=+15
        // flight2: FF=+20, TTG=25, Landing=+45 initially
        // After recompute with new FF=+5: flight2 has FF=+5, Landing=+30
        // If positioned by FF: flight2 (+5), flight1 (+10) - flight2 moves to front
        // If positioned by Landing: flight1 (+15), flight2 (+30) - flight2 stays behind
        // This proves it uses FF for positioning

        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1Ttg = TimeSpan.FromMinutes(5);
        var flight2Ttg = TimeSpan.FromMinutes(25);

        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithTrajectory(new Trajectory(flight1Ttg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(now.AddMinutes(20))
            .WithTrajectory(new Trajectory(flight2Ttg))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var trajectoryService = new MockTrajectoryService()
            .WithTrajectoryForFlight(flight1, new Trajectory(flight1Ttg))
            .WithTrajectoryForFlight(flight2, new Trajectory(flight2Ttg));

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithRunwayMode(_runwayMode)
                .WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial state
        flight1.LandingEstimate.ShouldBe(now.AddMinutes(15), "flight1 landing estimate = FF + TTG = 10 + 5");
        flight2.LandingEstimate.ShouldBe(now.AddMinutes(45), "flight2 landing estimate = FF + TTG = 20 + 25");

        var handler = GetRequestHandler(instanceManager, sequence, trajectoryService);

        // Change flight2's feeder fix estimate to be earlier than flight1
        // This makes flight2's FF earlier (+5 < +10) but landing later (+30 > +15)
        // Update the Fixes array to simulate the estimate update (this is what RecomputeRequestHandler reads)
        flight2.Fixes = [
            new FixEstimate("RIVET", now.AddMinutes(5)),
            new FixEstimate("YSSY", now.AddMinutes(30))
        ];

        var request = new RecomputeRequest("YSSY", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Verify flight2's new landing estimate is still later than flight1
        flight2.LandingEstimate.ShouldBe(now.AddMinutes(30), "flight2 landing estimate = new FF + TTG = 5 + 25");
        flight2.LandingEstimate.ShouldBeGreaterThan(flight1.LandingEstimate, "flight2 lands later than flight1");

        // Despite landing later, flight2 should be positioned first because its FF estimate is earlier
        sequence.Flights[0].Callsign.ShouldBe("QFA2", "flight2 should be first (positioned by FF estimate +5)");
        sequence.Flights[1].Callsign.ShouldBe("QFA1", "flight1 should be second (positioned by FF estimate +10)");
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithSingleRunway("34L", TimeSpan.FromSeconds(180))
                .WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, trajectoryService);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Change the landing estimate of the last flight to be earlier than the second flight
        // Update the Fixes array (this is what RecomputeRequestHandler reads)
        var newFeederFixEstimate = now.AddMinutes(-8);
        flight3.Fixes = [
            new FixEstimate("RIVET", newFeederFixEstimate),
            new FixEstimate("YSSY", newFeederFixEstimate.Add(_defaultTtg))
        ];

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
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Flight 3 should be behind flight 1 separated by acceptance rate");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Flight 2 should be behind flight 3 separated by acceptance rate");
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act

        // Re-route the flight to a new feeder fix
        var newFeederFixEstimate = now.AddMinutes(3);
        flight.Fixes = [new FixEstimate("WELSH", newFeederFixEstimate), new FixEstimate("TESAT", landingTime)];

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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var handler = GetRequestHandler(instanceManager, sequence, trajectoryService, mediator);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, trajectoryService);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([_airportConfiguration]);

        var trajectoryService = new MockTrajectoryService();
        var mediator = Substitute.For<IMediator>();
        var logger = Substitute.For<Serilog.ILogger>();

        var handler = new RecomputeRequestHandler(
            instanceManager,
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
        IMaestroInstanceManager instanceManager,
        Sequence sequence,
        ITrajectoryService? trajectoryService = null,
        IMediator? mediator = null)
    {
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([_airportConfiguration]);

        trajectoryService ??= new MockTrajectoryService();
        mediator ??= Substitute.For<IMediator>();

        var logger = Substitute.For<Serilog.ILogger>();

        return new RecomputeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            trajectoryService,
            clockFixture.Instance,
            mediator,
            logger);
    }
}
