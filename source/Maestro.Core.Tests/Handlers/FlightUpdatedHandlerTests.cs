using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightUpdatedHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    [Fact]
    public async Task WhenANewFlightIsAdded_ItIsSequenced()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            false,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);
        
        var scheduler = Substitute.For<IScheduler>();
        var handler = GetHandler(sequence, clock, scheduler);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        scheduler.Received(1).Schedule(sequence, Arg.Is<Flight>(f => f.Callsign == notification.Callsign));
    }
    
    [Fact]
    public async Task WhenANewFlightIsUpdated_AndOutOfRangeOfFeederFix_TheFlightIsNotTracked()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            false,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);

        var handler = GetHandler(sequence, clock);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task WhenAnActivatedFlightIsUpdated_AndOutOfRangeOfFeederFix_TheFlightIsNotTracked()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);

        var handler = GetHandler(sequence, clock);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task WhenAnInactiveFlightIsUpdated_AndWithinRangeOfFeederFix_TheFlightIsTracked()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            false,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);

        var handler = GetHandler(sequence, clock);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe("QFA123");
        flight.Activated.ShouldBe(false);
    }
    
    [Fact]
    public async Task WhenAnActivatedFlightIsUpdated_AndWithinRangeOfFeederFix_TheFlightIsTracked()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);

        var handler = GetHandler(sequence, clock);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe("QFA123");
        flight.Activated.ShouldBe(true);
    }
    
    [Fact]
    public async Task WhenAnExistingFlightIsActivated_TheFlightIsActivated()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            false,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))]);

        var handler = GetHandler(sequence, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Sanity check
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe("QFA123");
        flight.Activated.ShouldBe(false);
        
        // Act
        var activatedTime = DateTimeOffset.UtcNow;
        clock.SetTime(activatedTime);
        notification = notification with
        {
            Activated = true
        };
        
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        flight = sequence.Flights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe("QFA123");
        flight.Activated.ShouldBe(true);
        flight.ActivatedTime.ShouldBe(activatedTime);
    }
    
    [Fact]
    public void WhenAnInactiveFlightIsUpdated_ItIsNotRecomputed()
    {
        Assert.Fail("Stub");
    }
    
    [Fact]
    public void WhenAnActiveFlightIsUpdated_ItIsRecomputed()
    {
        Assert.Fail("Stub");
    }

    [Fact]
    public void WhenAFlightIsNotTrackingViaAFeederFix_ItIsAddedToPenidng()
    {
        Assert.Fail("Stub");
    }
    
    [Fact]
    public void WhenARunwayIsAssigned_TheHighestPriorityRunwayIsChosen()
    {
        Assert.Fail("Stub");
    }
    
    [Fact]
    public void WhenARunwayIsAssigned_AndEstimateIsBeforeARunwayChange_ExistingRunwayIsChosen()
    {
        Assert.Fail("Stub");
    }
    
    [Fact]
    public void WhenARunwayIsAssigned_AndEstimateIsAfterARunwayChange_NewRunwayIsChosen()
    {
        Assert.Fail("Stub");
    }

    [Fact]
    public async Task WhenUnstableFlightIsWithinThreshold_ButNotMinUnstableTime_ItStaysUnstable()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(2))]);

        var handler = GetHandler(sequence, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Advance time past the minimum unstable time
        clock.SetTime(clock.UtcNow().AddMinutes(1));
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        // Active for 1 minute, 1 minute to FF. Still unstable due to minimum unstable time.
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Unstable);
    }

    [Fact]
    public async Task WhenUnstableFlightIsWithinThreshold_ItIsStablised()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20))]);

        var handler = GetHandler(sequence, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Advance time past the minimum unstable time
        clock.SetTime(clock.UtcNow().AddMinutes(3));
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Stable);
    }

    [Fact]
    public async Task WhenStableFlightHasPassedInitialFeederFixEstimate_ItIsSuperStablised()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(30))
            ]);

        var handler = GetHandler(sequence, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Advance time past the minimum unstable time
        clock.SetTime(clock.UtcNow().AddMinutes(10));
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.SuperStable);
    }

    [Fact]
    public async Task WhenFlightIsWithinThreshold_ItIsFrozen()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(-10)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(5))
            ]);

        var handler = GetHandler(sequence, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Advance time past the minimum unstable time
        clock.SetTime(clock.UtcNow().AddMinutes(3));
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Frozen);
    }

    [Fact]
    public async Task WhenFlightHasPassedLandingTime_ItIsMarkedAsLanded()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34L",
            true,
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(-10)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(5))
            ]);

        var handler = GetHandler(sequence, clock);
        await handler.Handle(notification, CancellationToken.None);
        
        // Advance time past the minimum unstable time
        clock.SetTime(clock.UtcNow().AddMinutes(5));
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Landed);
    }

    [Fact]
    public async Task WhenAFlightNeedsRecomputing_AndItHasBeenRerouted_TheFeederFixIsUpdated()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .Build();
        
        sequence.Add(flight);
        
        // Change the feeder fix (emulate a re-route)
        var newEtaFf = DateTimeOffset.Now.AddMinutes(5);
        var notification = new FlightUpdatedNotification(
            "QFA1",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "ODALE7",
            "34L",
            true,
            null,
            [
                new FixEstimate("AKMIR", newEtaFf),
                new FixEstimate("YSSY", newEtaFf.AddMinutes(10))
            ]);

        var handler = GetHandler(sequence, clock);
        
        // Act
        flight.NeedsRecompute = true;
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        flight.FeederFixIdentifier.ShouldBe("AKMIR");
        flight.EstimatedFeederFixTime.ShouldBe(newEtaFf);
        flight.NeedsRecompute.ShouldBe(false);
    }

    // TODO: Verify this behavior is desired.
    [Fact]
    public async Task WhenAFlightNeedsRecomputing_AndARunwayHasBeenManuallyAssigned_ItIsNotOverridden()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .Build();
        
        sequence.Add(flight);
        
        // Change the runway
        flight.SetRunway("34R", manual: true);
        
        var notification = new FlightUpdatedNotification(
            "QFA1",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "RIVET4",
            "34R",
            true,
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(5)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(10))
            ]);

        var handler = GetHandler(sequence, clock);
        
        // Act
        flight.NeedsRecompute = true;
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
        flight.RunwayManuallyAssigned.ShouldBe(true);
        flight.NeedsRecompute.ShouldBe(false);
    }

    [Fact]
    public async Task WhenAFlightNeedsRecomputing_AndACustomFeederFixEstimateWasProvided_ItIsOverridden()
    {
        await Task.CompletedTask;
        Assert.Fail("Stub");
    }

    FlightUpdatedHandler GetHandler(Sequence sequence, IClock clock, IScheduler? scheduler = null)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.CanSequenceFor(Arg.Is("YSSY")).Returns(true);
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var runwayAssigner = Substitute.For<IRunwayAssigner>();
        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        var estimateProvider = Substitute.For<IEstimateProvider>();
        scheduler ??= Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();
        
        return new FlightUpdatedHandler(
            sequenceProvider,
            runwayAssigner,
            rateLimiter,
            estimateProvider,
            scheduler,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}