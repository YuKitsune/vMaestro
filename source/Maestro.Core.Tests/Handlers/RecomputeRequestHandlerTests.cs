using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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

    readonly RunwayMode _runwayMode = new()
    {
        Identifier = "34IVA",
        Runways =
        [
            new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = 180 },
            new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = 180 }
        ]
    };

    [Fact]
    public async Task WhenRecomputingAFlight_TheSequenceIsRecalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetRequestHandler(sequence, scheduler: scheduler);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        scheduler.Received(1).Schedule(sequence);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRecomputingAFlight_LandingTimeIsResetToEstimatedTime(bool manual)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var scheduledLandingTime = now.AddMinutes(15);
        var estimatedLandingTime = now.AddMinutes(10);

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(scheduledLandingTime, manual)
            .WithLandingEstimate(estimatedLandingTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ScheduledLandingTime.ShouldBe(estimatedLandingTime);
        flight.ManualLandingTime.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRecomputingAFlight_FeederFixTimeIsResetToEstimatedTime(bool manual)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var scheduledFeederFixTime = now.AddMinutes(15);
        var manualFeederFixEstaimate = now.AddMinutes(10);

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithFeederFixTime(scheduledFeederFixTime)
            .WithFeederFixEstimate(manualFeederFixEstaimate, manual)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var actualFeederFixEstaimate = now.AddMinutes(5);
        var estimateProvider = new MockEstimateProvider(
            feederFixEstimate: actualFeederFixEstaimate,
            landingEstimate: flight.EstimatedLandingTime);

        var handler = GetRequestHandler(sequence, estimateProvider);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ScheduledFeederFixTime.ShouldBe(actualFeederFixEstaimate);
        flight.EstimatedFeederFixTime.ShouldBe(actualFeederFixEstaimate);
        flight.ManualFeederFixEstimate.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenRecomputingAFlightWithNewFeederFix_FeederFixAndEstimatesAreUpdated()
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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act

        // Re-route the flight to a new feeder fix
        var newFeederFixEstimate = now.AddMinutes(3);
        flight.Fixes = [new FixEstimate("WELSH", newFeederFixEstimate), new FixEstimate("TESAT", landingTime)];

        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.FeederFixIdentifier.ShouldBe("WELSH");
        flight.ScheduledFeederFixTime.ShouldBe(newFeederFixEstimate);
        // TODO: flight.ManualFeederFixTime.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRecomputingAFlight_RunwayIsReset(bool manual)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R", manual)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34L"); // Default runway from runway mode
        flight.RunwayManuallyAssigned.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenRecomputingAFlightWithNoFeederFix_FlightIsMarkedAsHighPriority()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFix("RIVET")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        flight.Fixes = [new FixEstimate("TESAT", now.AddMinutes(10))]; // No feeder fix
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.HighPriority.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenRecomputingAFlight_NoDelayIsResetToFalse()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .NoDelay()
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.NoDelay.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenRecomputingAFlightThatDoesNotExist_ReturnsEmptyResponse()
    {
        // Arrange
        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "NONEXISTENT");

        // Act
        var response = await handler.Handle(request, CancellationToken.None);

        // Assert
        response.ShouldNotBeNull();
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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var mediator = Substitute.For<IMediator>();
        var handler = GetRequestHandler(sequence, mediator: mediator);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        await mediator.Received(1).Publish(
            Arg.Is<SequenceUpdatedNotification>(n => n.AirportIdentifier == "YSSY"),
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
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(expectedState);
    }

    RecomputeRequestHandler GetRequestHandler(
        Sequence sequence,
        IEstimateProvider? estimateProvider = null,
        IScheduler? scheduler = null,
        IMediator? mediator = null)
    {
        var sequenceProvider = new MockSequenceProvider(sequence);
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([_airportConfiguration]);

        estimateProvider ??= CreateEstimateProvider();
        scheduler ??= Substitute.For<IScheduler>();
        mediator ??= Substitute.For<IMediator>();

        var logger = Substitute.For<Serilog.ILogger>();

        return new RecomputeRequestHandler(
            sequenceProvider,
            airportConfigurationProvider,
            estimateProvider,
            clockFixture.Instance,
            scheduler,
            mediator,
            logger);

        IEstimateProvider CreateEstimateProvider()
        {
            var estimateProvider = Substitute.For<IEstimateProvider>();
            estimateProvider.GetLandingEstimate(Arg.Any<Flight>(), Arg.Any<DateTimeOffset?>())
                .Returns(call => call.Arg<Flight>().EstimatedLandingTime);
            estimateProvider.GetFeederFixEstimate(Arg.Any<AirportConfiguration>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<FlightPosition>())
                .Returns(call => call.Arg<DateTimeOffset>());
            return estimateProvider;
        }
    }
}
