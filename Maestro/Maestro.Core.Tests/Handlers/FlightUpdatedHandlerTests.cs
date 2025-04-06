using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Handlers;
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
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            false,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(3))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator);
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
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            true,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(3))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator);
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
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            false,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator);
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
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            true,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator);
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
            "YMML",
            "YSSY",
            "34L",
            "RIVET1",
            false,
            [new FixDto("RIVET", new Coordinate(0, 0), clock.UtcNow().AddHours(1))]);

        var mediator = Substitute.For<IMediator>();
        
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        var sequence = CreateTestSequence(mediator);
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

    Sequence CreateTestSequence(IMediator mediator)
    {
        return new Sequence(
            "YSSY",
            [
                new RunwayMode
                {
                    Identifier = "34",
                    LandingRates = new Dictionary<string, TimeSpan>()
                }
            ],
            [new FixConfigurationDto { Identifier = "RIVET", Latitude = 0, Longitude = 0 }],
            mediator);
    }
}