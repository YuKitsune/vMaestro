using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests.Model;

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
    public void WhenAFlightIsNew_ItRemainsUnstable()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithActivationTime(clockFixture.Instance.UtcNow()) // Just activated
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(5)) // Within stable threshold
            .WithState(State.Unstable)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance);

        // Assert
        flight.State.ShouldBe(State.Unstable);
    }

    [Fact]
    public void WhenAFlightIsWithinRange_ItIsStabilised()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithActivationTime(clockFixture.Instance.UtcNow().AddMinutes(-30))
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(25)) // Within stable threshold of 25 minutes
            .WithFeederFixTime(clockFixture.Instance.UtcNow().AddMinutes(26)) // Scheduled time slightly out
            .WithLandingTime(clockFixture.Instance.UtcNow().AddMinutes(40))
            .WithState(State.Unstable)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance);

        // Assert
        flight.State.ShouldBe(State.Stable);
    }

    [Fact]
    public void WhenAFlightPassesTheOriginalFeederFixEstimate_ItIsSuperStabilised()
    {
        // Arrange - Create a flight that has passed its original feeder fix time
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow()) // Reached feeder fix time
            .WithFeederFixTime(clockFixture.Instance.UtcNow().AddMinutes(2)) // Schedueld time slightly out
            .WithLandingTime(clockFixture.Instance.UtcNow().AddMinutes(40))
            .WithState(State.Stable)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance);

        // Assert
        flight.State.ShouldBe(State.SuperStable);
    }

    [Fact]
    public void WhenAFlightIsWithinRange_ItIsFrozen()
    {
        // Arrange - Create a flight within 15 minutes of landing
        var flight = new FlightBuilder("QFA1")
            .WithLandingTime(clockFixture.Instance.UtcNow().AddMinutes(15)) // Within frozen threshold of 15 minutes
            .WithState(State.SuperStable)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance);

        // Assert
        flight.State.ShouldBe(State.Frozen);
    }

    [Fact]
    public void WhenAFlightLands_ItIsMarkedAsLanded()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingTime(clockFixture.Instance.UtcNow()) // Scheduled time slightly out
            .WithState(State.Frozen)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance);

        // Assert
        flight.State.ShouldBe(State.Landed);
    }
}
