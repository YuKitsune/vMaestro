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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var newEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newEstimate);
        var handler = GetHandler(instanceManager, sequence);

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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var expectedLandingEstimate = clockFixture.Instance.UtcNow().AddMinutes(25);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetLandingEstimate(flight, Arg.Any<DateTimeOffset>())
            .Returns(expectedLandingEstimate);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(instanceManager, sequence, estimateProvider: estimateProvider);

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
        var newLandingEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetLandingEstimate(flight1, Arg.Any<DateTimeOffset>())
            .Returns(newLandingEstimate);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(instanceManager, sequence, estimateProvider: estimateProvider);

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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(instanceManager, sequence);

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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(instanceManager, sequence);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Change the feeder fix estimate

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }

    ChangeFeederFixEstimateRequestHandler GetHandler(
        IMaestroInstanceManager instanceManager,
        Sequence sequence,
        IEstimateProvider? estimateProvider = null,
        IMediator? mediator = null,
        ILogger? logger = null)
    {
        estimateProvider ??= Substitute.For<IEstimateProvider>();
        mediator ??= Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();

        return new ChangeFeederFixEstimateRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            estimateProvider,
            clockFixture.Instance,
            mediator,
            logger);
    }
}
