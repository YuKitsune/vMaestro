using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Shouldly;

namespace Maestro.Core.Tests;

public class FlightTests
{
    readonly DateTimeOffset landingTime = new DateTimeOffset(2025, 05, 21, 06,49, 00, TimeSpan.Zero);
    
    [Fact]
    public void WhenAFlightIsDelayed_AndItSlowsDown_DelayReduces()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingTime)
            .WithLandingTime(landingTime.AddMinutes(5))
            .WithState(State.Stable)
            .Build();
        
        // Sanity check
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after slowing down
        flight.UpdateLandingEstimate(landingTime.AddMinutes(2));
        
        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(3));
        
        // Act: New estimate after slowing down
        flight.UpdateLandingEstimate(landingTime.AddMinutes(5));
        
        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenAFlightIsDelayed_AndItSpeedsUp_DelayIncreases()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingTime)
            .WithLandingTime(landingTime.AddMinutes(5))
            .WithState(State.Stable)
            .Build();
        
        // Sanity check
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after speeding up
        flight.UpdateLandingEstimate(landingTime.AddMinutes(-2));
        
        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(7));
        
        // Act: New estimate after slowing down
        flight.UpdateLandingEstimate(landingTime.AddMinutes(-5));
        
        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void WhenAFlightIsDelayed_AndItSlowsDownTooMuch_DelayIsNegative()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingTime)
            .WithLandingTime(landingTime.AddMinutes(5))
            .WithState(State.Stable)
            .Build();
        
        // Sanity check
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after speeding up
        flight.UpdateLandingEstimate(landingTime.AddMinutes(8));
        
        // Assert
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingDelay.ShouldBe(TimeSpan.FromMinutes(-3));
    }
}