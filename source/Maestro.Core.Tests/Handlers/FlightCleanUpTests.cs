using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class SequenceCleanerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    private readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public void WhenAFlightHasLanded_ItIsNotImmediatelyDeleted()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow())
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

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
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow().AddMinutes(-5))
            .Build();

        var sequenceBuilder = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight);

        for (var i = 0; i < 5; i++)
        {
            var num = i + 2;
            var newFlight = new FlightBuilder($"QFA{num}")
                .WithState(State.Landed)
                .WithLandingTime(clock.UtcNow().AddMinutes(-5 + i))
                .Build();

            sequenceBuilder.WithFlight(newFlight);
        }

        var sequence = sequenceBuilder.Build();

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());

        // Act
        cleaner.CleanUpFlights(sequence);

        // Assert
        sequence.Flights.Count.ShouldBe(5, "only one flight should have been removed");
        sequence.Flights.ShouldNotContain(flight, "the first flight to land should be the first one to be removed");
    }

    [Fact]
    public void WhenAFlightHasLanded_AndTimeHasPassed_ItIsDeleted()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithLandingTime(clock.UtcNow().AddMinutes(-15))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

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
        var clock = clockFixture.Instance;
        var now = clock.UtcNow();

        var flight = new FlightBuilder("QFA1")
            .WithLastSeen(now)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());

        // Act
        cleaner.CleanUpFlights(sequence);

        // Assert
        sequence.Flights.ShouldContain(flight, "The flight was seen recently and should not be deleted");
    }
}
