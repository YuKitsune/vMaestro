using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightCleanUpTests
{
    private readonly AirportConfiguration _airportConfiguration;

    public FlightCleanUpTests(AirportConfigurationFixture airportConfigurationFixture)
    {
        _airportConfiguration = airportConfigurationFixture.Instance;
    }

    [Fact]
    public async Task WhenAFlightHasLanded_ItIsNotImmediatelyDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow())
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var handler = new FlightCleanUp(sequenceProvider, clock, Substitute.For<ILogger>());
        
        // Act
        await handler.Handle(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldContain(flight);
    }
    
    [Fact]
    public async Task WhenAFlightHasLanded_AndMoreThanFiveLandedFlightsHaveLanded_OlderFlightsAreDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow().AddMinutes(-5))
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        for (var i = 0; i < 5; i++)
        {
            var num = i + 2;
            var newFlight = new FlightBuilder($"QFA{num}")
                .WithState(State.Landed)
                .WithLandingTime(clock.UtcNow().AddMinutes(-5 + i))
                .Build();
            
            sequence.Add(newFlight);
        }

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var handler = new FlightCleanUp(sequenceProvider, clock, Substitute.For<ILogger>());
        
        // Act
        await handler.Handle(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), CancellationToken.None);
        
        // Assert
        sequence.Flights.Length.ShouldBe(5, "only one flight should have been removed");
        sequence.Flights.ShouldNotContain(flight, "the first flight to land should be the first one to be removed");
    }
    
    [Fact]
    public async Task WhenAFlightHasLanded_AndTimeHasPassed_ItIsDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow().AddMinutes(-15))
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var handler = new FlightCleanUp(sequenceProvider, clock, Substitute.For<ILogger>());
        
        // Act
        await handler.Handle(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldBeEmpty("landed flights should be removed at STA+15");
    }
    
    [Fact]
    public async Task WhenAFlightHasBeenSeenRecently_ItIsNotDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var now = clock.UtcNow();
        
        var flight = new FlightBuilder("QFA1")
            .WithLastSeen(now)
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var handler = new FlightCleanUp(sequenceProvider, clock, Substitute.For<ILogger>());
        
        // Act
        await handler.Handle(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldContain(flight, "The flight was seen recently and should not be deleted");
    }
    
    [Fact]
    public async Task WhenAFlightHasNotBeenSeenRecently_ItIsDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var now = clock.UtcNow();
        
        var flight = new FlightBuilder("QFA1")
            .WithLastSeen(now.AddHours(-1))
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var handler = new FlightCleanUp(sequenceProvider, clock, Substitute.For<ILogger>());
        
        // Act
        await handler.Handle(new MaestroFlightUpdatedNotification(flight.ToMessage(sequence)), CancellationToken.None);
        
        // Assert
        sequence.Flights.ShouldNotContain(flight, "The flight has not been seen recently");
    }
}