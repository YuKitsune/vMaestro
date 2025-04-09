using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightUpdatedHandlerTests
{
    [Fact]
    public async Task WhenANewFlightIsUpdated_AndOutOfRangeOfFeederFix_TheFlightIsNotTracked()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            false,
            null,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(3))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator, clock);
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);


        var handler = new FlightUpdatedHandler(
            sequenceProvider,
            mediator,
            clock);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task WhenAnActivatedFlightIsUpdated_AndOutOfRangeOfFeederFix_TheFlightIsNotTracked()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            true,
            null,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(3))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator, clock);
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);


        var handler = new FlightUpdatedHandler(
            sequenceProvider,
            mediator,
            clock);
        
        // Act
        await handler.Handle(notification, CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task WhenANewFlightIsUpdated_AndWithinRangeOfFeederFix_TheFlightIsTracked()
    {
        // Arrange
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            false,
            null,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator, clock);
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);

        var handler = new FlightUpdatedHandler(
            sequenceProvider,
            mediator,
            clock);
        
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
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            true,
            null,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator, clock);
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);

        var handler = new FlightUpdatedHandler(
            sequenceProvider,
            mediator,
            clock);
        
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
        
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            false,
            null,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator, clock);
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);

        var handler = new FlightUpdatedHandler(
            sequenceProvider,
            mediator,
            clock);
        
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

    Sequence CreateTestSequence(IMediator mediator, IClock clock)
    {
        return new Sequence(
            new AirportConfigurationDto(
                "YSSY",
                [
                    new RunwayConfigurationDto("34L", 180),
                    new RunwayConfigurationDto("34R", 180)
                ],
                [
                    new RunwayModeConfigurationDto(
                        "34IVA",
                        TimeSpan.FromSeconds(30),
                        [
                            new RunwayConfigurationDto("34L", 180),
                            new RunwayConfigurationDto("34R", 180)
                        ],
                        new Dictionary<string, TimeSpan>
                            {
                                {"34L", TimeSpan.FromSeconds(180)},
                                {"34R", TimeSpan.FromSeconds(180)}
                            },
                        [])
                ],
                [new ViewConfigurationDto("BIK", null, null, LadderReferenceTime.FeederFixTime)],
                ["RIVET"]),
            new FixedSeparationProvider(TimeSpan.FromMinutes(2)),
            new FixedPerformanceLookup(),
            mediator,
            clock);
    }
}