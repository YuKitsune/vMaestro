using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ManualDelayRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenFlightDoesNotExist_ThrowsException()
    {
        // Arrange
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();
        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA999", 5);

        // Act & Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task MaximumDelayIsAssignedToFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(15))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA1", 8);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.MaximumDelay.ShouldBe(TimeSpan.FromMinutes(8));
    }

    [Fact]
    public async Task WhenFlightHasNoDelay_SequenceRemainsUnchanged()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var originalLandingTime = flight1.LandingTime;
        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA1", 5);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalLandingTime);
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
    }

    [Fact]
    public async Task WhenFlightHasDelayBelowMaximum_SequenceRemainsUnchanged()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(13)) // 3-minute delay
            .Build();
        var originalLandingTime = flight2.LandingTime;

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA1", 5); // Maximum delay is 5 minutes

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.LandingTime.ShouldBe(originalLandingTime);
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
    }

    [Fact]
    public async Task WhenFlightHasDelayAboveMaximum_FlightIsRepositionedToReduceDelay()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(13))
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(19))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA3", 5); // Maximum delay is 5 minutes

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight3.MaximumDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight3.TotalDelay.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));

        // Landing order should be updated
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight3).ShouldBe(2);
        sequence.NumberInSequence(flight2).ShouldBe(3);

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate);
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
    }

    [Fact]
    public async Task WhenAssigningZeroDelay_DelayIsNoMoreThanRunwayAcceptanceRate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(13))
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(19))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA3", 0); // Zero delay

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight3.MaximumDelay.ShouldBe(TimeSpan.Zero);
        flight3.TotalDelay.ShouldBeLessThan(airportConfigurationFixture.AcceptanceRate);

        // Landing order should be updated
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight3).ShouldBe(2);
        sequence.NumberInSequence(flight2).ShouldBe(3);

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate);
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
    }

    [Fact]
    public async Task WhenPreceedingFlightsAreFrozen_FlightIsRepositionedAfterLastFrozenFlight()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithState(State.SuperStable)
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(18))
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(21))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA3", 0);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight3.MaximumDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Landing order should be updated
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight3).ShouldBe(2);
        sequence.NumberInSequence(flight2).ShouldBe(3);

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate);
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
    }

    ManualDelayRequestHandler GetHandler(
        IMaestroInstanceManager instanceManager,
        Sequence sequence,
        IMediator? mediator = null)
    {
        mediator ??= Substitute.For<IMediator>();

        return new ManualDelayRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());
    }
}
