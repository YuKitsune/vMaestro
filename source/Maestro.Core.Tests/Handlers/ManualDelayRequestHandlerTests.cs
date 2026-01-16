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
            .WithLandingTime(now.AddMinutes(12))
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(15))
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
        flight3.MaximumDelay.ShouldBe(TimeSpan.FromMinutes(0));
        flight3.TotalDelay.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(3));

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

    [Fact(Skip = "Inaccurate behaviour")]
    public async Task WhenPrecedingFlightsAreFrozen_FlightIsRepositionedAfterLastFrozenFlight()
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
        flight3.MaximumDelay.ShouldBe(TimeSpan.FromMinutes(0));

        // Landing order should be updated
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight3).ShouldBe(2);
        sequence.NumberInSequence(flight2).ShouldBe(3);

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate);
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
    }

    [Fact]
    public async Task WhenPrecedingFlightHasManualDelayWithEarlierETA_CurrentFlightCannotMovePastThem()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .ManualDelay(TimeSpan.Zero)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(13))
            .ManualDelay(TimeSpan.FromMinutes(5))
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(16))
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(19))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);
        sequence.NumberInSequence(flight4).ShouldBe(4);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA4", 0); // Max 5 minute delay for flight4

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Flight3 cannot move past flight1 because flight1 has an earlier ETA (10) than flight3 (12)
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should remain as it has the earliest ETA");
        sequence.NumberInSequence(flight4).ShouldBe(2, "flight4 should move ahead of flight2 and 3 as it has the next earliest ETA with a manual delay");
        sequence.NumberInSequence(flight2).ShouldBe(3, "flight2 should give way to flight4");
        sequence.NumberInSequence(flight3).ShouldBe(4);

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate);
        flight4.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight2.LandingTime.ShouldBe(flight4.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
    }

    [Fact]
    public async Task WhenPrecedingFlightHasManualDelayWithLaterETA_CurrentFlightCanMovePastThem()
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
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .ManualDelay(TimeSpan.Zero)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(18))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA3", 0); // 0 delay for flight3

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Flight3 should move past flight1 because flight3's ETA (10) is earlier than flight1's ETA (15)
        sequence.NumberInSequence(flight1).ShouldBe(1, "flight1 should remain first");
        sequence.NumberInSequence(flight3).ShouldBe(2, "flight3 should move ahead of flight2 as its ETA is earlier");
        sequence.NumberInSequence(flight2).ShouldBe(3, "flight2 should give way to flight3");

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate);
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate));
    }

    // TODO: Assert on exception instead

    [Fact]
    public async Task WhenManualDelayFlightETAIsWithinSlot_FlightCannotMoveIntoSlot()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        // Create a slot that covers the time period where flight3's ETA falls
        var slotStart = now.AddMinutes(5);
        var slotEnd = now.AddMinutes(15);
        sequence.CreateSlot(slotStart, slotEnd, [flight.AssignedRunwayIdentifier]);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA1", 0); // Zero delay

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingTime.ShouldBe(slotEnd, "manual delay flights cannot move into a slot, even if the maximum delay is exceeded");
    }

    // TODO: Assert on exception instead

    [Fact]
    public async Task WhenManualDelayFlightETAIsWithinRunwayModeChange_FlightCannotMoveIntoChangePeriod()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        // Schedule a runway mode change where flight3's ETA falls within the change period
        var lastLandingTimeForOldMode = now.AddMinutes(5);
        var firstLandingTimeForNewMode = now.AddMinutes(15);
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways = [new RunwayConfiguration
            {
                Identifier = "34R",
                LandingRateSeconds = (int)airportConfigurationFixture.AcceptanceRate.TotalSeconds
            }]
        });
        sequence.ChangeRunwayMode(newRunwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode);

        var handler = GetHandler(instanceManager, sequence);
        var request = new ManualDelayRequest("YSSY", "QFA1", 0); // Zero delay

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingTime.ShouldBe(firstLandingTimeForNewMode, "manual delay flights cannot move into a configuration change period, even if the maximum delay is exceeded");
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
