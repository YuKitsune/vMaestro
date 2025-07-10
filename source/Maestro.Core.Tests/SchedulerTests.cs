﻿using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests;

public class SchedulerTests
{
    static TimeSpan _landingRate = TimeSpan.FromSeconds(180);
    static IClock _clock = new FixedClock(DateTimeOffset.Now);

    readonly AirportConfigurationFixture _airportConfigurationFixture;
    readonly IScheduler _scheduler;

    public SchedulerTests(AirportConfigurationFixture airportConfigurationFixture)
    {
        _airportConfigurationFixture = airportConfigurationFixture;

        var lookup = Substitute.For<IPerformanceLookup>();
        lookup.GetPerformanceDataFor(Arg.Any<string>()).Returns(x =>
            new AircraftPerformanceData
            {
                Type = x.ArgAt<string>(0),
                AircraftCategory = AircraftCategory.Jet,
                WakeCategory = WakeCategory.Medium
            });

        _scheduler = new Scheduler(lookup, Substitute.For<ILogger>());
    }

    [Fact]
    public void SingleFlight_IsNotDelayed()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(flight);

        // Act
        _scheduler.Schedule(sequence, flight);

        // Assert
        flight.ScheduledFeederFixTime.ShouldBe(flight.EstimatedFeederFixTime);
        flight.ScheduledLandingTime.ShouldBe(flight.EstimatedLandingTime);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
        flight.RemainingDelay.ShouldBe(TimeSpan.Zero);
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


        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.AddBlockout(blockout);
        sequence.Add(flight);

        // Act
        _scheduler.Schedule(sequence, flight);

        // Assert
        flight.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
        flight.ScheduledLandingTime.ShouldBe(endTime);
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
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

        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(12))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(22))
            .WithRunway("34L")
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.AddBlockout(blockout);
        sequence.Add(firstFlight);
        sequence.Add(secondFlight);

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);

        firstFlight.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
        firstFlight.ScheduledLandingTime.ShouldBe(endTime);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));

        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime!.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(3).Add(_landingRate));
    }

    [Fact]
    public void NewFlight_EarlierThanStable_StableFlightIsDelayed()
    {
        // Arrange
        var stableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithFeederFixTime(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(stableFlight);

        // First pass
        _scheduler.Schedule(sequence, stableFlight);

        // New flight added with an earlier ETA
        var unstableFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Add(unstableFlight);

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);

        unstableFlight.ScheduledFeederFixTime.ShouldBe(unstableFlight.EstimatedFeederFixTime);
        unstableFlight.ScheduledLandingTime.ShouldBe(unstableFlight.EstimatedLandingTime);
        unstableFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        stableFlight.ScheduledFeederFixTime.ShouldBe(unstableFlight.ScheduledFeederFixTime!.Value.Add(_landingRate));
        stableFlight.ScheduledLandingTime.ShouldBe(unstableFlight.ScheduledLandingTime.Add(_landingRate));
        stableFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));
    }

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

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(firstFlight);
        sequence.Add(secondFlight);

        // First pass
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Sanity check
        firstFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        firstFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime!.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));

        // Update the ETA for the second flight so that it should be earlier than the first one
        firstFlight.SetState(State.Stable);
        secondFlight.UpdateFeederFixEstimate(firstFlight.EstimatedFeederFixTime.Value.Add(TimeSpan.FromMinutes(-1)));
        secondFlight.UpdateLandingEstimate(firstFlight.EstimatedLandingTime.Add(TimeSpan.FromMinutes(-1)));

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Assert
        // First flight should not be delayed
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);

        firstFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        firstFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Second flight should be delayed
        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime!.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(4));
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

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(firstFlight);

        // First pass
        _scheduler.Schedule(sequence, firstFlight);
        firstFlight.SetState(existingState);

        // New flight added with an earlier ETA
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(newFlightState)
            .Build();

        sequence.Add(secondFlight);

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);

        firstFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        firstFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime!.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(4));
    }

    [Fact]
    public void MultipleFlights_SameRunway_SeparatedByRunwayRate()
    {
        // Arrange
        var first = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var third = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(first);
        sequence.Add(second);
        sequence.Add(third);

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2", "QFA3"]);

        // First result shouldn't have any delay
        first.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);

        // Remaining results should each be separated by landing rate
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime!.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));

        third.ScheduledFeederFixTime.ShouldBe(second.ScheduledFeederFixTime!.Value.Add(_landingRate));
        third.ScheduledLandingTime.ShouldBe(second.ScheduledLandingTime.Add(_landingRate));
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

        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34R")
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(first);
        sequence.Add(second);

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        // Assert
        first.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);
        first.TotalDelay.ShouldBe(TimeSpan.Zero);

        second.ScheduledFeederFixTime.ShouldBe(second.EstimatedFeederFixTime);
        second.ScheduledLandingTime.ShouldBe(second.EstimatedLandingTime);
        second.TotalDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void MultipleFlights_WithNoConflicts_ShouldNotBeDelayed()
    {
        // Arrange
        var farAwayFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddHours(10))
            .WithLandingEstimate(_clock.UtcNow().AddHours(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var closeFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var veryCloseFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(farAwayFlight);
        sequence.Add(closeFlight);
        sequence.Add(veryCloseFlight);

        // Act
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }

        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA3", "QFA2", "QFA1"]);

        farAwayFlight.ScheduledFeederFixTime.ShouldBe(farAwayFlight.EstimatedFeederFixTime);
        farAwayFlight.ScheduledLandingTime.ShouldBe(farAwayFlight.EstimatedLandingTime);

        closeFlight.ScheduledFeederFixTime.ShouldBe(closeFlight.EstimatedFeederFixTime);
        closeFlight.ScheduledLandingTime.ShouldBe(closeFlight.EstimatedLandingTime);

        veryCloseFlight.ScheduledFeederFixTime.ShouldBe(veryCloseFlight.EstimatedFeederFixTime);
        veryCloseFlight.ScheduledLandingTime.ShouldBe(veryCloseFlight.EstimatedLandingTime);
    }

    [Fact]
    public void WhenReroutedToAnotherFix_EstimatesAreStillCalculatedToFeederFix()
    {
        // TODO:
        //  Create a flight
        //  Schedule
        //  Reroute to another feeder fix
        //  Check estimates are calculated based on trajectory to previous FF
        Assert.Fail("Stub");
    }

    [Fact]
    public void WhenUnstableFlightIsRecomputed_ItsPositionInSequenceChanges()
    {
        // TODO:
        //  Create two flights with separate ETA_FF
        //  Swap ETA_FF so second flight is now in front (leap frog)
        //  Check both flights receive no delay and position in sequence is updated
        Assert.Fail("Stub");
    }

    [Fact]
    public void SuperStableFlights_DoNotChangePositionInSequence()
    {
        // TODO:
        //  Create multiple flights with separate ETA_FF
        //  Jumble ETAs (leap frog)
        //  Check delays are updated and position in sequence is unchanged
        Assert.Fail("Stub");
    }

    [Theory]
    [InlineData(State.Desequenced)]
    [InlineData(State.Removed)]
    public void WhenAFlightIsNotSequencable_ItDoesNotAffectOtherFlights(State state)
    {
        // Arrange
        var first = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(first);
        sequence.Add(second);

        // First pass, normal sequence
        sequence.Schedule(_scheduler);

        // Sanity check, first flight is not delayed
        first.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);

        // Second result should now be delayed
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));

        // Act
        first.Desequence();
        sequence.Schedule(_scheduler);

        // Assert

        // Second flight shouldn't have any delay
        second.ScheduledFeederFixTime.ShouldBe(second.ScheduledFeederFixTime);
        second.ScheduledLandingTime.ShouldBe(second.EstimatedLandingTime);
    }

    [Fact]
    public void WhenADesequencedFlightIsResumed_TheSequenceIsRecomputed()
    {
        // Arrange
        var first = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(first);
        sequence.Add(second);

        // First pass, normal sequence
        sequence.Schedule(_scheduler);

        // Desequence the first flight and recompute the sequence
        first.Desequence();
        sequence.Schedule(_scheduler);

        // Sanity check, second flight should no longer have a delay
        second.ScheduledFeederFixTime.ShouldBe(second.ScheduledFeederFixTime);
        second.ScheduledLandingTime.ShouldBe(second.EstimatedLandingTime);

        // Act
        first.Resume();
        sequence.Schedule(_scheduler);

        // Assert

        // First result shouldn't have any delay
        first.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);

        // Second result should now be delayed again
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public void WhenADesequencedFlightIsResumed_InFrontOfAStableFlight_ResumedFlightIsSequencedBehindStableFlight()
    {
        // Arrange
        var first = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(first);
        sequence.Add(second);

        // First pass, normal sequence
        sequence.Schedule(_scheduler);

        // Desequence the first flight and recompute the sequence
        first.Desequence();
        sequence.Schedule(_scheduler);

        // Act
        // Stabilize the second flight and resume the first one
        second.SetState(State.Stable);
        first.Resume();
        sequence.Schedule(_scheduler);

        // Assert
        // No delay for the second flight since it was stabilized first
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
        second.ScheduledFeederFixTime.ShouldBe(second.EstimatedFeederFixTime);
        second.ScheduledLandingTime.ShouldBe(second.EstimatedLandingTime);

        // First flight (resumed) should now be behind
        first.ScheduledFeederFixTime.ShouldBe(second.ScheduledFeederFixTime.Value.Add(_landingRate));
        first.ScheduledLandingTime.ShouldBe(second.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public void WhenDelayingAFlight_InConflictWithManualLandingTimeFlight_TheFlightIsFurtherDelayed()
    {
        // Arrange
        var leadingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var subjectFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(21))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var fixedLandingTime = _clock.UtcNow().AddMinutes(25);
        var fixedFlight = new FlightBuilder("QFA3")
            .WithLandingTime(fixedLandingTime, manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(leadingFlight);
        sequence.Add(subjectFlight);
        sequence.Add(fixedFlight);

        // Act
        sequence.Schedule(_scheduler);

        // Assert
        // No delay for the first flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA3", "QFA2"]);
        leadingFlight.ScheduledFeederFixTime.ShouldBe(leadingFlight.EstimatedFeederFixTime);
        leadingFlight.ScheduledLandingTime.ShouldBe(leadingFlight.EstimatedLandingTime);

        // Fixed flight should have no change to its schedule
        fixedFlight.ScheduledLandingTime.ShouldBe(fixedLandingTime);

        // Subject flight should be delayed behind the fixed flight since delaying it behind the leading flight
        // puts it in conflict with the fixed flight
        subjectFlight.ScheduledLandingTime.ShouldBe(fixedLandingTime.Add(_landingRate));
    }

    [Fact]
    public void WhenDelayingAFlight_InConflictWithMultipleManualLandingTimeFlights_TheFlightIsFurtherDelayed()
    {
        // Arrange
        var leadingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var subjectFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(21))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var fixedLandingTime1 = _clock.UtcNow().AddMinutes(25);
        var fixedFlight1 = new FlightBuilder("QFA3")
            .WithLandingTime(fixedLandingTime1, manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var fixedLandingTime2 = _clock.UtcNow().AddMinutes(28);
        var fixedFlight2 = new FlightBuilder("QFA4")
            .WithLandingTime(fixedLandingTime2, manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(leadingFlight);
        sequence.Add(subjectFlight);
        sequence.Add(fixedFlight1);
        sequence.Add(fixedFlight2);

        // Act
        sequence.Schedule(_scheduler);

        // Assert
        // No delay for the first flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA3", "QFA4", "QFA2"]);
        leadingFlight.ScheduledFeederFixTime.ShouldBe(leadingFlight.EstimatedFeederFixTime);
        leadingFlight.ScheduledLandingTime.ShouldBe(leadingFlight.EstimatedLandingTime);

        // Fixed flights should have no change to their schedules
        fixedFlight1.ScheduledLandingTime.ShouldBe(fixedLandingTime1);
        fixedFlight2.ScheduledLandingTime.ShouldBe(fixedLandingTime2);

        // Subject flight should be delayed behind the fixed flights since delaying it behind the leading flight
        // puts it in conflict with the fixed flights
        subjectFlight.ScheduledLandingTime.ShouldBe(fixedLandingTime2.Add(_landingRate));
    }

    [Fact]
    public void WhenDelayingAFlight_InConflictWithNoDelayFlight_TheFlightIsFurtherDelayed()
    {
        // Arrange
        var leadingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var subjectFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(21))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var fixedFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(25))
            .NoDelay()
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(leadingFlight);
        sequence.Add(subjectFlight);
        sequence.Add(fixedFlight);

        // Act
        sequence.Schedule(_scheduler);

        // Assert
        // No delay for the first flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA3", "QFA2"]);
        leadingFlight.ScheduledFeederFixTime.ShouldBe(leadingFlight.EstimatedFeederFixTime);
        leadingFlight.ScheduledLandingTime.ShouldBe(leadingFlight.EstimatedLandingTime);

        // Fixed flight should have no change to its schedule
        fixedFlight.ScheduledLandingTime.ShouldBe(fixedFlight.EstimatedLandingTime);

        // Subject flight should be delayed behind the fixed flight since delaying it behind the leading flight
        // puts it in conflict with the fixed flight
        subjectFlight.ScheduledLandingTime.ShouldBe(fixedFlight.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public void WhenDelayingAFlight_InConflictWithMultipleNoDelayFlights_TheFlightIsFurtherDelayed()
    {
        // Arrange
        var leadingFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var subjectFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(21))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var fixedFlight1 = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(25))
            .NoDelay()
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var fixedFlight2 = new FlightBuilder("QFA4")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(28))
            .NoDelay()
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        sequence.Add(leadingFlight);
        sequence.Add(subjectFlight);
        sequence.Add(fixedFlight1);
        sequence.Add(fixedFlight2);

        // Act
        sequence.Schedule(_scheduler);

        // Assert
        // No delay for the first flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA3", "QFA4", "QFA2"]);
        leadingFlight.ScheduledFeederFixTime.ShouldBe(leadingFlight.EstimatedFeederFixTime);
        leadingFlight.ScheduledLandingTime.ShouldBe(leadingFlight.EstimatedLandingTime);

        // Fixed flights should have no change to their schedules
        fixedFlight1.ScheduledLandingTime.ShouldBe(fixedFlight1.EstimatedLandingTime);
        fixedFlight2.ScheduledLandingTime.ShouldBe(fixedFlight2.EstimatedLandingTime);

        // Subject flight should be delayed behind the fixed flights since delaying it behind the leading flight
        // puts it in conflict with the fixed flights
        subjectFlight.ScheduledLandingTime.ShouldBe(fixedFlight2.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public void PriorityFlights_AreNotDelayed()
    {
        Assert.Fail("Stub");
    }

    [Fact]
    public void ZeroDelayFlights_AreNotDelayed()
    {
        Assert.Fail("Stub");
    }

    [Fact]
    public void LandedFlights_CanBeReInsertedIntoTheSequence()
    {
        Assert.Fail("Stub");
    }
}
