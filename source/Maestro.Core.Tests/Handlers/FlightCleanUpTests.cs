using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class SequenceCleanerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public void WhenAFlightHasLanded_ItIsNotImmediatelyDeleted()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture, clockFixture)
            .WithFlight(flight)
            .Build();

        var cleaner = new SequenceCleaner(clockFixture.Instance, Substitute.For<ILogger>());

        // Act
        cleaner.CleanUpFlights(sequence);

        // Assert
        sequence.Flights.ShouldNotContain(flight);
        sequence.LandedFlights.ShouldContain(flight);
    }

    [Fact]
    public void WhenAFlightHasLanded_AndMoreThanFiveLandedFlightsHaveLanded_OlderFlightsAreDeleted()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture, clockFixture)
            .WithFlight(flight)
            .Build();

        for (var i = 1; i <= 6; i++)
        {
            var newFlight = new FlightBuilder($"QFA{i}")
                .WithState(State.Landed)
                .Build();

            sequence.Slots[i].AllocateTo(newFlight);
        }

        clock.SetTime(sequence.Flights.Last().ScheduledLandingTime);

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());

        // Act
        cleaner.CleanUpFlights(sequence);

        // Assert
        sequence.Flights.ShouldBeEmpty("all flights should have landed");
        sequence.LandedFlights.Count.ShouldBe(5, "only 5 landed flights should be kept");
        sequence.LandedFlights.ShouldNotContain(flight, "the earliest landed flight should be removed");
    }

    [Fact]
    public void WhenAFlightHasLanded_AndTimeHasPassed_ItIsDeleted()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture, clockFixture)
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

        var sequence = new SequenceBuilder(airportConfigurationFixture, clockFixture)
            .WithFlight(flight)
            .Build();

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
        var clock = clockFixture.Instance;
        var now = clock.UtcNow();

        var flight = new FlightBuilder("QFA1")
            .WithLastSeen(now.AddHours(-1))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture, clockFixture)
            .WithFlight(flight)
            .Build();

        var cleaner = new SequenceCleaner(clock, Substitute.For<ILogger>());

        // Act
        cleaner.CleanUpFlights(sequence);

        // Assert
        sequence.Flights.ShouldNotContain(flight, "The flight has not been seen recently");
    }
}
