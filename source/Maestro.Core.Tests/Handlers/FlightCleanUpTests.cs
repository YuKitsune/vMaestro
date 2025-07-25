﻿using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class SequenceCleanerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    private readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public void WhenAFlightHasLanded_ItIsNotImmediatelyDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow())
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());
        
        // Act
        cleaner.CleanUpFlights(sequence);
        
        // Assert
        sequence.Flights.ShouldContain(flight);
    }
    
    [Fact]
    public void WhenAFlightHasLanded_AndMoreThanFiveLandedFlightsHaveLanded_OlderFlightsAreDeleted()
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

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());
        
        // Act
        cleaner.CleanUpFlights(sequence);
        
        // Assert
        sequence.Flights.Length.ShouldBe(5, "only one flight should have been removed");
        sequence.Flights.ShouldNotContain(flight, "the first flight to land should be the first one to be removed");
    }
    
    [Fact]
    public void WhenAFlightHasLanded_AndTimeHasPassed_ItIsDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow().AddMinutes(-15))
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());
        
        // Act
        cleaner.CleanUpFlights(sequence);
        
        // Assert
        sequence.Flights.ShouldBeEmpty("landed flights should be removed at STA+15");
    }
    
    [Fact]
    public void WhenAFlightHasBeenSeenRecently_ItIsNotDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var now = clock.UtcNow();
        
        var flight = new FlightBuilder("QFA1")
            .WithLastSeen(now)
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());
        
        // Act
        cleaner.CleanUpFlights(sequence);
        
        // Assert
        sequence.Flights.ShouldContain(flight, "The flight was seen recently and should not be deleted");
    }
    
    [Fact]
    public void WhenAFlightHasNotBeenSeenRecently_ItIsDeleted()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));
        var now = clock.UtcNow();
        
        var flight = new FlightBuilder("QFA1")
            .WithLastSeen(now.AddHours(-1))
            .Build();
        
        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());
        
        // Act
        cleaner.CleanUpFlights(sequence);
        
        // Assert
        sequence.Flights.ShouldNotContain(flight, "The flight has not been seen recently");
    }
}