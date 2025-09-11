using Maestro.Core.Handlers;
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var newEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newEstimate);
        var handler = GetHandler(sequence);

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
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var expectedLandingEstimate = clockFixture.Instance.UtcNow().AddMinutes(25);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetLandingEstimate(flight, Arg.Any<DateTimeOffset>())
            .Returns(expectedLandingEstimate);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(sequence, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        estimateProvider.Received(1).GetLandingEstimate(flight, Arg.Any<DateTimeOffset>());
        flight.LandingEstimate.ShouldBe(expectedLandingEstimate);
    }

    [Fact]
    public async Task WhenChangingFeederFixEstimate_SequenceIsRecalculated()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var scheduler = Substitute.For<IScheduler>();
        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(sequence, scheduler: scheduler);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        scheduler.Received(1).Recompute(flight, sequence);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(sequence);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable);
    }

    ChangeFeederFixEstimateRequestHandler GetHandler(
        Sequence sequence,
        IEstimateProvider? estimateProvider = null,
        IScheduler? scheduler = null,
        IMediator? mediator = null,
        ILogger? logger = null)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        estimateProvider ??= Substitute.For<IEstimateProvider>();
        scheduler ??= Substitute.For<IScheduler>();
        mediator ??= Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();

        return new ChangeFeederFixEstimateRequestHandler(
            sessionManager,
            estimateProvider,
            scheduler,
            clockFixture.Instance,
            mediator,
            logger);
    }
}
