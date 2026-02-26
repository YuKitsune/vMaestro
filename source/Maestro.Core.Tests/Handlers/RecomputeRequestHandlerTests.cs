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
    public async Task TheFlightIsMovedBasedOnItsLandingEstimate()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        // Change the landing estimate of the second flight to be earlier than the first flight
        flight2.UpdateFeederFixEstimate(now.AddMinutes(5).Subtract(_defaultTtg));

        var request = new RecomputeRequest("YSSY", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights[0].Callsign.ShouldBe("QFA2", "First flight should be QFA2 after recompute");
        sequence.Flights[1].Callsign.ShouldBe("QFA1", "Second flight should be QFA1 after recompute");
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithSingleRunway("34L", TimeSpan.FromSeconds(180)).WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Change the landing estimate of the last flight to be earlier than the second flight
        flight3.UpdateFeederFixEstimate(now.AddMinutes(12).Subtract(_defaultTtg));

        var request = new RecomputeRequest("YSSY", "QFA3");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "Second flight should be QFA3 (moved forward)");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "Third flight should be QFA2");

        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "First flight's landing time should be unchanged");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "First flight should land at its estimate");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Flight 3 should be behind flight 1 separated by acceptance rate");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Flight 2 should be behind flight 3 separated by acceptance rate");
    }

    // TODO: Check STA_FF and ETA_FF

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

    // TODO: Revisit
    [Fact]
    public async Task RunwayIsReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
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
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var handler = GetRequestHandler(instanceManager, sequence, mediator: mediator);
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
        DateTimeOffset landingEstimate;

        // Configure times based on expected state
        switch (expectedState)
        {
            case State.Unstable:
                feederFixEstimate = now.AddMinutes(35); // More than 25 minutes to feeder fix
                landingEstimate = now.AddMinutes(65);
                break;
            case State.Stable:
                feederFixEstimate = now.AddMinutes(20); // Within 25 minutes of feeder fix
                landingEstimate = now.AddMinutes(50);
                break;
            case State.SuperStable:
                feederFixEstimate = now.AddMinutes(-5); // Past initial feeder fix time
                landingEstimate = now.AddMinutes(25);
                break;
            case State.Frozen:
                feederFixEstimate = now.AddMinutes(10); // Within 15 minutes of landing
                landingEstimate = now.AddMinutes(10);
                break;
            case State.Landed:
                feederFixEstimate = now.AddMinutes(5); // Past scheduled landing time
                landingEstimate = now.AddMinutes(-5);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedState));
        }

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixTime(now.AddMinutes(-20))
            .WithLandingTime(now.AddMinutes(1))
            .WithFeederFixEstimate(feederFixEstimate)
            .WithLandingEstimate(landingEstimate)
            .WithActivationTime(now.Subtract(TimeSpan.FromMinutes(10)))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithRunwayMode(_runwayMode).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
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

        var trajectoryService = Substitute.For<ITrajectoryService>();
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

        trajectoryService ??= Substitute.For<ITrajectoryService>();
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
