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

    [Fact]
    public async Task TheFlightIsMovedBasedOnItsLandingEstimate()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create two stable flights, one after the other
        // TODO: Change the landing estimate of the second flight to be earlier than the first flight

        // Act
        // TODO: Recompute the second flight

        // Assert
        // TODO: Assert the landing order is second, first
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three stable flights, one after the other
        // TODO: Change the landing estimate of the last flight to be earlier than the second flight

        // Act
        // TODO: Recompute the last flight

        // Assert
        // TODO: Assert the landing order is first, last, second
        // TODO: Assert the first flight's landing time is unchanged
        // TODO: Assert the last flight's landing time is its landing estimate
        // TODO: Assert the second flight's landing time is after the last flight's landing time plus separation
    }

    // TODO: Check STA_FF and ETA_FF

    [Fact]
    public async Task ManualFeederFixEstimateIsRemoved()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a stable flight with a manual feeder fix estimate

        // Act
        // TODO: Recompute the flight

        // Assert
        // TODO: Assert the flight's manual feeder fix estimate is removed
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
        flight.FeederFixTime.ShouldBe(newFeederFixEstimate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunwayIsReset(bool manual)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R", manual)
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
        flight.RunwayManuallyAssigned.ShouldBeFalse();
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
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Move a flight

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }

    RecomputeRequestHandler GetRequestHandler(
        IMaestroInstanceManager instanceManager,
        Sequence sequence,
        IEstimateProvider? estimateProvider = null,
        IMediator? mediator = null)
    {
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([_airportConfiguration]);

        estimateProvider ??= CreateEstimateProvider();
        mediator ??= Substitute.For<IMediator>();

        var logger = Substitute.For<Serilog.ILogger>();

        return new RecomputeRequestHandler(
            instanceManager,
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
