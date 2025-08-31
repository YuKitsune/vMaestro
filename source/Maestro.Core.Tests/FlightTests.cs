﻿using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests;

public class FlightTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _landingTime = clockFixture.Instance.UtcNow();

    [Fact]
    public void WhenAFlightIsDelayed_AndItSlowsDown_DelayReduces()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_landingTime)
            .WithLandingTime(_landingTime.AddMinutes(5))
            .WithState(State.Stable)
            .Build();

        // Sanity check
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after slowing down
        flight.UpdateLandingEstimate(_landingTime.AddMinutes(2));

        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(3));

        // Act: New estimate after slowing down
        flight.UpdateLandingEstimate(_landingTime.AddMinutes(5));

        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenAFlightIsDelayed_AndItSpeedsUp_DelayIncreases()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_landingTime)
            .WithLandingTime(_landingTime.AddMinutes(5))
            .WithState(State.Stable)
            .Build();

        // Sanity check
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after speeding up
        flight.UpdateLandingEstimate(_landingTime.AddMinutes(-2));

        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(7));

        // Act: New estimate after slowing down
        flight.UpdateLandingEstimate(_landingTime.AddMinutes(-5));

        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void WhenAFlightIsDelayed_AndItSlowsDownTooMuch_DelayIsNegative()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_landingTime)
            .WithLandingTime(_landingTime.AddMinutes(5))
            .WithState(State.Stable)
            .Build();

        // Sanity check
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after speeding up
        flight.UpdateLandingEstimate(_landingTime.AddMinutes(8));

        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(-3));
    }

    [Fact]
    public void WhenOrderingFlights_TheyAreOrderedCorrectly()
    {
        // Arrange
        var referenceTime = new DateTimeOffset(2025, 05, 25, 00, 00, 00, TimeSpan.Zero);

        // Stable flights should be ordered by state and scheduled time
        var landedFlight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var landedFlight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime)
            .WithState(State.Landed)
            .Build();

        var frozenFlight1 = new FlightBuilder("QFA3")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(5))
            .WithState(State.Frozen)
            .Build();

        var frozenFlight2 = new FlightBuilder("QFA4")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(10))
            .WithState(State.Frozen)
            .Build();

        var superStableFlight1 = new FlightBuilder("QFA5")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(15))
            .WithState(State.SuperStable)
            .Build();

        var superStableFlight2 = new FlightBuilder("QFA6")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(20))
            .WithState(State.SuperStable)
            .Build();

        var stableFlight1 = new FlightBuilder("QFA7")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(25))
            .WithState(State.Stable)
            .Build();

        var stableFlight2 = new FlightBuilder("QFA8")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(30))
            .WithState(State.Stable)
            .Build();

        // Unstable flights should be ordered by estimates
        var unstableFlight1 = new FlightBuilder("QFA9")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(35))
            .WithState(State.Unstable)
            .Build();

        var unstableFlight2 = new FlightBuilder("QFA10")
            .WithLandingEstimate(referenceTime.AddMinutes(5))
            .WithLandingTime(referenceTime.AddMinutes(40))
            .WithState(State.Unstable)
            .Build();

        // Act
        var sortedSet = new SortedSet<Flight>
        {
            frozenFlight2,
            landedFlight1,
            stableFlight1,
            stableFlight2,
            unstableFlight1,
            superStableFlight2,
            unstableFlight2,
            superStableFlight1,
            frozenFlight1,
            landedFlight2
        };

        // Assert
        sortedSet.Select(f => f.Callsign)
            .ShouldBe([
                "QFA1",
                "QFA2",
                "QFA3",
                "QFA4",
                "QFA5",
                "QFA6",
                "QFA7",
                "QFA8",
                "QFA9",
                "QFA10"
            ]);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public void WhenOrderingFlights_TheyAreOrderedByScheduledLandingTime(State state)
    {
        // Arrange
        var referenceTime = new DateTimeOffset(2025, 05, 25, 00, 00, 00, TimeSpan.Zero);

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(20))
            .WithState(state)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(10))
            .WithState(state)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime.AddMinutes(15))
            .WithState(state)
            .Build();

        // Act
        var sorted = new SortedSet<Flight> { flight1, flight2, flight3 };

        // Assert
        sorted.Select(f => f.Callsign).ShouldBe(["QFA2", "QFA3", "QFA1"]);
    }

    [Fact]
    public void WhenOrderingFlightsWithSameTime_TheyAreOrderedByStatePriority()
    {
        var referenceTime = new DateTimeOffset(2025, 05, 25, 00, 00, 00, TimeSpan.Zero);

        var unstable = new FlightBuilder("QFA1")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime)
            .WithState(State.Unstable)
            .Build();

        var landed = new FlightBuilder("QFA2")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime)
            .WithState(State.Landed)
            .Build();

        var frozen = new FlightBuilder("QFA3")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime)
            .WithState(State.Frozen)
            .Build();

        var superStable = new FlightBuilder("QFA4")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime)
            .WithState(State.SuperStable)
            .Build();

        var stable = new FlightBuilder("QFA5")
            .WithLandingEstimate(referenceTime)
            .WithLandingTime(referenceTime)
            .WithState(State.Stable)
            .Build();

        var sorted = new SortedSet<Flight> { unstable, frozen, superStable, stable, landed };

        // Order should follow: Landed, Frozen, SuperStable, Stable, Unstable
        sorted.Select(f => f.Callsign).ShouldBe(["QFA2", "QFA3", "QFA4", "QFA5", "QFA1"]);
    }

    [Fact]
    public void WhenAFlightHasBeenRemoved_ItCannotBeResumed()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .Build();

        // Act
        flight.Remove();

        // Assert
        var action = () => flight.SetState(State.Stable, clockFixture.Instance);
        action.ShouldThrow<MaestroException>();
    }
}
