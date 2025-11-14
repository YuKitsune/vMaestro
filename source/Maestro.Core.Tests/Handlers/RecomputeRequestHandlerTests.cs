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

// TODO: All of these tests asserting landing times need to be moved to a separate scheduler test.
// Handlers tests should just assert the position is set.

public class RecomputeRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    readonly RunwayMode _runwayMode = new(
        new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            Runways =
            [
                new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = 180 },
                new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = 180 }
            ]
        });

    // TODO: When recomputing a flight, and it moves to a later time, the sequence is recalculated from where the flight was
    // TODO: When recomputing a flight, and it moved to an earlier time, the sequence is recalculated from where the flight moves to

    [Fact]
    public async Task WhenRecomputingAFlight_TheSequenceIsRecalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();

        // Insert flights in order based on landing estimates
        sequence.Insert(flight2, flight2.LandingEstimate); // QFA2 first (10 min)
        sequence.Insert(flight1, flight1.LandingEstimate); // QFA1 second (20 min)

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA2 should be first initially");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA1 should be second initially");

        // Change QFA1's estimate to be earlier than QFA2
        flight1.UpdateLandingEstimate(now.AddMinutes(5));

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - QFA1 should now be first due to earlier estimate
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA1 should be first after recompute with earlier estimate");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA2 should be second after QFA1 moves ahead");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "QFA1 landing time should be reset to estimate");
    }

    // TODO: Nope. The landing time should be re-calculated.

    [Fact]
    public async Task WhenRecomputingAFlight_LandingTimeIsResetToEstimatedTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var scheduledLandingTime = now.AddMinutes(15);
        var estimatedLandingTime = now.AddMinutes(10);

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(estimatedLandingTime)
            .WithLandingTime(scheduledLandingTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.InitialLandingEstimate.ShouldBe(estimatedLandingTime);
        flight.LandingEstimate.ShouldBe(estimatedLandingTime);
        flight.LandingTime.ShouldBe(estimatedLandingTime);
    }

    // TODO: Nope.

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
            .WithFeederFixEstimate(manualFeederFixEstaimate, manual)
            .WithFeederFixTime(scheduledFeederFixTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var actualFeederFixEstaimate = now.AddMinutes(5);
        var estimateProvider = new MockEstimateProvider(
            feederFixEstimate: actualFeederFixEstaimate,
            landingEstimate: flight.LandingEstimate);

        var handler = GetRequestHandler(sequence, estimateProvider);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.InitialFeederFixEstimate.ShouldBe(actualFeederFixEstaimate);
        flight.FeederFixEstimate.ShouldBe(actualFeederFixEstaimate);
        flight.FeederFixTime.ShouldBe(actualFeederFixEstaimate);
        flight.ManualFeederFixEstimate.ShouldBeFalse();
    }

    // TODO: Check STA_FF and ETA_FF

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
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act

        // Re-route the flight to a new feeder fix
        var newFeederFixEstimate = now.AddMinutes(3);
        flight.Fixes = [new FixEstimate("WELSH", newFeederFixEstimate), new FixEstimate("TESAT", landingTime)];

        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.FeederFixIdentifier.ShouldBe("WELSH");
        flight.FeederFixTime.ShouldBe(newFeederFixEstimate);
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
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34L"); // Default runway from runway mode
        flight.RunwayManuallyAssigned.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenRecomputingAFlight_MaximumDelayIsReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .ManualDelay(TimeSpan.FromMinutes(5))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.MaximumDelay.ShouldBeNull();
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
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

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
            .WithActivationTime(now.Subtract(TimeSpan.FromMinutes(10)))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

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
        IMediator? mediator = null)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([_airportConfiguration]);

        estimateProvider ??= CreateEstimateProvider();
        mediator ??= Substitute.For<IMediator>();

        var logger = Substitute.For<Serilog.ILogger>();

        return new RecomputeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            estimateProvider,
            clockFixture.Instance,
            mediator,
            logger);

        IEstimateProvider CreateEstimateProvider()
        {
            var estimateProvider = Substitute.For<IEstimateProvider>();
            estimateProvider.GetLandingEstimate(Arg.Any<Flight>(), Arg.Any<DateTimeOffset?>())
                .Returns(call => call.Arg<Flight>().LandingEstimate);
            estimateProvider.GetFeederFixEstimate(Arg.Any<AirportConfiguration>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<FlightPosition>())
                .Returns(call => call.Arg<DateTimeOffset>());
            return estimateProvider;
        }
    }
}
