using System.Runtime.CompilerServices;
using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class SchedulerTests
{
    static TimeSpan _minimumTimeBetweenArrivals = TimeSpan.FromMinutes(1);
    static TimeSpan _landingRate = TimeSpan.FromSeconds(180);
    static TimeSpan _staggerRate = TimeSpan.FromSeconds(30);
    static IClock _clock = new FixedClock(DateTimeOffset.Now);
    
    readonly RunwayModeConfiguration _runwayModeConfiguration;
    readonly IScheduler _scheduler;

    public SchedulerTests()
    {
        _runwayModeConfiguration = new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            StaggerRateSeconds = (int) _staggerRate.TotalSeconds,
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

        _scheduler = new Scheduler(separationRuleProvider);
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
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
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
        
        var second = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var third = new FlightBuilder("QFA1")
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
    public void MultipleFlights_DifferentRunway_SeparatedByStaggerRate()
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
        secondResult.ScheduledFeederFixTime.ShouldBe(firstResult.ScheduledFeederFixTime.Value.Add(_staggerRate));
        secondResult.ScheduledLandingTime.ShouldBe(firstResult.ScheduledLandingTime.Add(_staggerRate));
    }

    [Fact]
    public void DirtyEstimates_ShouldNotRuinTheSequence()
    {
        // Arrange
        var dirtyFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddHours(10))
            .WithLandingEstimate(_clock.UtcNow().AddHours(20))
            .WithRunway("34L")
            .Build();
        
        var cleanFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34R")
            .Build();
        
        // Act
        var result = _scheduler.ScheduleFlights(
                [dirtyFlight, cleanFlight],
                [],
                _runwayModeConfiguration)
            .OrderBy(f => f.ScheduledFeederFixTime)
            .ToArray();
        
        // First result shouldn't have any delay
        var firstResult = result.First();
        firstResult.Callsign.ShouldBe("QFA2");
        firstResult.ScheduledFeederFixTime.ShouldBe(cleanFlight.EstimatedFeederFixTime);
        firstResult.ScheduledLandingTime.ShouldBe(cleanFlight.EstimatedLandingTime);

        var secondResult = result.Last();
        secondResult.Callsign.ShouldBe("QFA1");
        secondResult.ScheduledFeederFixTime.ShouldBe(dirtyFlight.ScheduledFeederFixTime.Value.Add(_staggerRate));
        secondResult.ScheduledLandingTime.ShouldBe(dirtyFlight.ScheduledLandingTime.Add(_staggerRate));
    }
}