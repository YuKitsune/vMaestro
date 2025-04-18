using System.Runtime.CompilerServices;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class SchedulerTests
{
    static TimeSpan _minimumTimeBetweenArrivals = TimeSpan.FromMinutes(1);
    static TimeSpan _landingRate = TimeSpan.FromSeconds(180);
    static IClock _clock = new FixedClock(DateTimeOffset.Now);
    
    readonly RunwayModeConfiguration _runwayModeConfiguration;
    readonly IScheduler _scheduler;

    public SchedulerTests()
    {
        _runwayModeConfiguration = new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    DefaultLandingRateSeconds = (int) _landingRate.TotalSeconds
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    DefaultLandingRateSeconds = (int) _landingRate.TotalSeconds
                }
            ]
        };
        
        var separationRuleProvider = Substitute.For<ISeparationRuleProvider>();
        separationRuleProvider
            .GetRequiredSpacing(Arg.Any<Flight>(), Arg.Any<Flight>())
            .Returns(_minimumTimeBetweenArrivals);

        _scheduler = new Scheduler(separationRuleProvider, new Logger<Scheduler>(NullLoggerFactory.Instance));
    }

    [Fact]
    public void SingleFlight_IsNotDelayed()
    {
        // Act
        var result = _scheduler.ScheduleFlights(
            [
                new FlightBuilder("QFA1")
                    .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
                    .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
                    .Build()
            ],
            [],
            _runwayModeConfiguration);
        
        // Assert
        var scheduledFlight = result.ShouldHaveSingleItem();
        scheduledFlight.ScheduledFeederFixTime.ShouldBe(scheduledFlight.EstimatedFeederFixTime);
        scheduledFlight.ScheduledLandingTime.ShouldBe(scheduledFlight.EstimatedLandingTime);
    }

    [Fact]
    public void SingleFlight_DuringBlockout_IsDelayed()
    {
        // Arrange
        var endTime = _clock.UtcNow().AddMinutes(25);
        var blockout = new BlockoutPeriod
        {
            RunwayIdentifier = "34L",
            StartTime = _clock.UtcNow().AddMinutes(5),
            EndTime = endTime,
        };
        
        // Act
        var result = _scheduler.ScheduleFlights(
            [
                new FlightBuilder("QFA1")
                    .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
                    .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
                    .WithRunway("34L")
                    .Build()
            ],
            [blockout],
            _runwayModeConfiguration);
        
        // Assert
        var scheduledFlight = result.ShouldHaveSingleItem();
        scheduledFlight.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
        scheduledFlight.ScheduledLandingTime.ShouldBe(endTime);
    }

    [Fact]
    public void MultipleFlights_DuringBlockout_AreDelayed()
    {
        // Arrange
        var endTime = _clock.UtcNow().AddMinutes(25);
        var blockout = new BlockoutPeriod
        {
            RunwayIdentifier = "34L",
            StartTime = _clock.UtcNow().AddMinutes(5),
            EndTime = endTime,
        };
        
        // Act
        var result = _scheduler.ScheduleFlights(
            [
                new FlightBuilder("QFA1")
                    .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
                    .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
                    .WithRunway("34L")
                    .Build(),
                new FlightBuilder("QFA2")
                    .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(12))
                    .WithLandingEstimate(_clock.UtcNow().AddMinutes(22))
                    .WithRunway("34L")
                    .Build()
            ],
            [blockout],
            _runwayModeConfiguration)
            .ToArray();

        // Assert
        var first = result.First();
        first.Callsign.ShouldBe("QFA1");
        first.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
        first.ScheduledLandingTime.ShouldBe(endTime);

        var second = result.Last();
        second.Callsign.ShouldBe("QFA2");
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public void NewFlight_EarlierThanStable_StableFlightIsDelayed()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithFeederFixTime(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();
        
        // First pass
        _scheduler.ScheduleFlights([firstFlight], [], _runwayModeConfiguration);
        
        // New flight added with an earlier ETA
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();
        
        // Act
        var result = _scheduler.ScheduleFlights(
            [firstFlight, secondFlight],
            [],
            _runwayModeConfiguration)
            .OrderBy(f => f.ScheduledLandingTime)
            .ToArray();
        
        // Assert
        var first = result.First();
        first.Callsign.ShouldBe("QFA2");
        first.ScheduledFeederFixTime.ShouldBe(secondFlight.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(secondFlight.EstimatedLandingTime);

        var second = result.Last();
        second.Callsign.ShouldBe("QFA1");
        second.ScheduledFeederFixTime.ShouldBe(secondFlight.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(secondFlight.ScheduledLandingTime.Add(_landingRate));
    }

    // TODO: Stable flights cannot change position unless a new flight is added with an earlier estimate
    
    [Fact]
    public void UnstableFlight_WithNewEstimates_EarlierThanStable_UnstableFlightIsDelayed()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(11))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(21))
            .WithRunway("34L")
            .Build();
        
        // First pass
        _scheduler.ScheduleFlights([firstFlight, secondFlight], [], _runwayModeConfiguration);
        
        // Sanity check
        firstFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        firstFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);
        
        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime.Add(_landingRate));
        
        // Update the ETA for the second flight so that it should be earlier than the first one
        firstFlight.SetState(State.Stable);
        secondFlight.UpdateFeederFixEstimate(firstFlight.EstimatedFeederFixTime.Value.Add(TimeSpan.FromMinutes(-1)));
        secondFlight.UpdateLandingEstimate(firstFlight.EstimatedLandingTime.Add(TimeSpan.FromMinutes(-1)));
        
        // Act
        var result = _scheduler.ScheduleFlights(
                [firstFlight, secondFlight],
                [],
                _runwayModeConfiguration)
            .OrderBy(f => f.ScheduledFeederFixTime)
            .ToArray();
        
        // Assert
        // First flight should not be delayed
        var first = result.First();
        first.Callsign.ShouldBe("QFA1");
        first.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);

        // Second flight should be delayed
        var second = result.Last();
        second.Callsign.ShouldBe("QFA2");
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));
    }
    
    [Theory]
    [InlineData(State.SuperStable, State.Unstable)]
    [InlineData(State.SuperStable, State.Stable)]
    [InlineData(State.Frozen, State.Unstable)]
    [InlineData(State.Frozen, State.Stable)]
    [InlineData(State.Landed, State.Unstable)]
    [InlineData(State.Landed, State.Stable)]
    public void NewFlight_EarlierThanSuperStable_NewFlightIsDelayed(State existingState, State newFlightState)
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        // First pass
        _scheduler.ScheduleFlights([firstFlight], [], _runwayModeConfiguration);
        firstFlight.SetState(existingState);
        
        // New flight added with an earlier ETA
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(newFlightState)
            .Build();
        
        // Act
        var result = _scheduler.ScheduleFlights(
                [firstFlight, secondFlight],
                [],
                _runwayModeConfiguration)
            .OrderBy(f => f.ScheduledFeederFixTime)
            .ToArray();
        
        // Assert
        var first = result.First();
        first.Callsign.ShouldBe("QFA1");
        first.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);

        var second = result.Last();
        second.Callsign.ShouldBe("QFA2");
        second.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public void MultipleFlights_SameRunway_SeparatedByRunwayRate()
    {
        // Arrange
        var first = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var third = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        // Act
        var result = _scheduler.ScheduleFlights(
                [first, second, third],
                [],
                _runwayModeConfiguration)
            .OrderBy(f => f.ScheduledFeederFixTime)
            .ToArray();
        
        // First result shouldn't have any delay
        var firstResult = result.First();
        firstResult.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        firstResult.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);

        // Remaining results should each be separated by lanidng rate
        var lastFeederFixTime = first.ScheduledFeederFixTime;
        var lastLandingTime = first.ScheduledLandingTime;
        foreach (var flight in result.Skip(1))
        {
            flight.ScheduledFeederFixTime.ShouldBe(lastFeederFixTime.Value.Add(_landingRate));
            flight.ScheduledLandingTime.ShouldBe(lastLandingTime.Add(_landingRate));

            lastFeederFixTime = flight.ScheduledFeederFixTime;
            lastLandingTime = flight.ScheduledLandingTime;
        }
    }

    [Fact]
    public void MultipleFlights_DifferentRunway_NotSeparated()
    {
        // Arrange
        var first = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var second = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34R")
            .Build();
        
        // Act
        var result = _scheduler.ScheduleFlights(
                [first, second],
                [],
                _runwayModeConfiguration)
            .OrderBy(f => f.ScheduledFeederFixTime)
            .ToArray();
        
        // First result shouldn't have any delay
        var firstResult = result.First();
        firstResult.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        firstResult.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);

        var secondResult = result.Last();
        secondResult.ScheduledFeederFixTime.ShouldBe(second.EstimatedFeederFixTime);
        secondResult.ScheduledLandingTime.ShouldBe(second.EstimatedLandingTime);
    }

    [Fact]
    public void MultipleFlights_WithNoConflicts_ShouldNotBeDelayed()
    {
        // Arrange
        var farAwayFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddHours(10))
            .WithLandingEstimate(_clock.UtcNow().AddHours(20))
            .WithRunway("34L")
            .Build();
        
        var closeFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var veryCloseFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(5))
            .WithRunway("34L")
            .Build();
        
        // Act
        var result = _scheduler.ScheduleFlights(
                [farAwayFlight, veryCloseFlight, closeFlight],
                [],
                _runwayModeConfiguration)
            .ToArray();
        
        var firstResult = result.Single(f => f.Callsign == "QFA1");
        firstResult.ScheduledFeederFixTime.ShouldBe(farAwayFlight.EstimatedFeederFixTime);
        firstResult.ScheduledLandingTime.ShouldBe(farAwayFlight.EstimatedLandingTime);
        
        var secondResult = result.Single(f => f.Callsign == "QFA2");
        secondResult.ScheduledFeederFixTime.ShouldBe(closeFlight.EstimatedFeederFixTime);
        secondResult.ScheduledLandingTime.ShouldBe(closeFlight.EstimatedLandingTime);
        
        var thirdResult = result.Single(f => f.Callsign == "QFA3");
        thirdResult.ScheduledFeederFixTime.ShouldBe(veryCloseFlight.EstimatedFeederFixTime);
        thirdResult.ScheduledLandingTime.ShouldBe(veryCloseFlight.EstimatedLandingTime);
    }
    
    // TODO: Scheduled times should not be earlier than the estimate
}