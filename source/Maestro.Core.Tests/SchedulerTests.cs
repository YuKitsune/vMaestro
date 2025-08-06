using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests;

public class SchedulerTests(
    PerformanceLookupFixture performanceLookupFixture,
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    static TimeSpan _landingRate = TimeSpan.FromSeconds(180);

    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;
    readonly IClock _clock = clockFixture.Instance;
    readonly IScheduler _scheduler = CreateScheduler(performanceLookupFixture, airportConfigurationFixture);

    static IScheduler CreateScheduler(PerformanceLookupFixture performanceLookupFixture, AirportConfigurationFixture airportConfigurationFixture)
    {
        var performanceLookup = performanceLookupFixture.Instance;
        var runwayAssigner = new RunwayAssigner(performanceLookup);
        var airportConfiguration = airportConfigurationFixture.Instance;

        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations()
            .Returns([airportConfiguration]);

        var lookup = Substitute.For<IPerformanceLookup>();
        lookup.GetPerformanceDataFor(Arg.Any<string>()).Returns(x =>
            new AircraftPerformanceData
            {
                Type = x.ArgAt<string>(0),
                AircraftCategory = AircraftCategory.Jet,
                WakeCategory = WakeCategory.Medium
            });

        return new Scheduler(
            runwayAssigner,
            airportConfigurationProvider,
            performanceLookup,
            Substitute.For<ILogger>());
    }

    [Fact]
    public void SingleFlight_IsNotDelayed()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        flight.ScheduledFeederFixTime.ShouldBe(flight.EstimatedFeederFixTime);
        flight.ScheduledLandingTime.ShouldBe(flight.EstimatedLandingTime);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
        flight.RemainingDelay.ShouldBe(TimeSpan.Zero);
    }

    // [Fact]
    // public void SingleFlight_DuringBlockout_IsDelayed()
    // {
    //     // Arrange
    //     var endTime = _clock.UtcNow().AddMinutes(25);
    //     var blockout = new BlockoutPeriod
    //     {
    //         RunwayIdentifier = "34L",
    //         StartTime = _clock.UtcNow().AddMinutes(5),
    //         EndTime = endTime,
    //     };
    //
    //
    //     var flight = new FlightBuilder("QFA1")
    //         .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
    //         .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
    //         .WithRunway("34L")
    //         .Build();
    //
    //     var sequence = new SequenceBuilder(_airportConfiguration)
    //         .WithFlight(flight)
    //         .Build();
    //     sequence.AddBlockout(blockout);
    //
    //     // Act
    //     _scheduler.Schedule(sequence);
    //
    //     // Assert
    //     flight.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
    //     flight.ScheduledLandingTime.ShouldBe(endTime);
    //     flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
    // }
    //
    // [Fact]
    // public void MultipleFlights_DuringBlockout_AreDelayed()
    // {
    //     // Arrange
    //     var endTime = _clock.UtcNow().AddMinutes(25);
    //     var blockout = new BlockoutPeriod
    //     {
    //         RunwayIdentifier = "34L",
    //         StartTime = _clock.UtcNow().AddMinutes(5),
    //         EndTime = endTime,
    //     };
    //
    //     var firstFlight = new FlightBuilder("QFA1")
    //         .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
    //         .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
    //         .WithRunway("34L")
    //         .Build();
    //
    //     var secondFlight = new FlightBuilder("QFA2")
    //         .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(12))
    //         .WithLandingEstimate(_clock.UtcNow().AddMinutes(22))
    //         .WithRunway("34L")
    //         .Build();
    //
    //     var sequence = new SequenceBuilder(_airportConfiguration)
    //         .WithFlight(firstFlight)
    //         .WithFlight(secondFlight)
    //         .Build();
    //     sequence.AddBlockout(blockout);
    //
    //     // Act
    //     _scheduler.Schedule(sequence);
    //
    //     // Assert
    //     sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);
    //
    //     firstFlight.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
    //     firstFlight.ScheduledLandingTime.ShouldBe(endTime);
    //     firstFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
    //
    //     secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime!.Value.Add(_landingRate));
    //     secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
    //     secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(3).Add(_landingRate));
    // }

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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(stableFlight)
            .Build();

        // First pass
        _scheduler.Schedule(sequence);

        // Verify initial state
        stableFlight.ScheduledLandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));

        // New flight added with an earlier ETA
        var unstableFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.New)
            .Build();

        sequence.AddFlight(unstableFlight, _scheduler);

        // Assert
        unstableFlight.State.ShouldBe(State.Unstable);
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);

        // New flight should not be delayed - it should keep its ETA
        unstableFlight.ScheduledFeederFixTime.ShouldBe(unstableFlight.EstimatedFeederFixTime);
        unstableFlight.ScheduledLandingTime.ShouldBe(unstableFlight.EstimatedLandingTime);
        unstableFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Stable flight should be delayed by landing rate
        stableFlight.ScheduledFeederFixTime.ShouldBe(unstableFlight.ScheduledFeederFixTime!.Value.Add(_landingRate));
        stableFlight.ScheduledLandingTime.ShouldBe(unstableFlight.ScheduledLandingTime.Add(_landingRate));
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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        // First pass
        _scheduler.Schedule(sequence);

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
        _scheduler.Schedule(sequence);

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
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public void NewFlight_EarlierThanFixed_NewFlightIsDelayed(State existingState)
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstFlight)
            .Build();

        // First pass
        _scheduler.Schedule(sequence);
        firstFlight.SetState(existingState);

        // New flight added with an earlier ETA
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.AddFlight(secondFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

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
            .WithState(State.Unstable)
            .Build();

        var second = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var third = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(first)
            .WithFlight(second)
            .WithFlight(third)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(first)
            .WithFlight(second)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(farAwayFlight)
            .WithFlight(closeFlight)
            .WithFlight(veryCloseFlight)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(first)
            .WithFlight(second)
            .Build();

        // First pass, normal sequence
        _scheduler.Schedule(sequence);

        // Sanity check, first flight is not delayed
        first.ScheduledFeederFixTime.ShouldBe(first.EstimatedFeederFixTime);
        first.ScheduledLandingTime.ShouldBe(first.EstimatedLandingTime);

        // Second result should now be delayed
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));

        // Act
        if (state == State.Desequenced)
        {
            sequence.DesequenceFlight(first.Callsign, _scheduler);
        }
        else if (state == State.Removed)
        {
            sequence.RemoveFlight(first.Callsign, _scheduler);
        }

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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(first)
            .WithFlight(second)
            .Build();

        // First pass, normal sequence
        _scheduler.Schedule(sequence);

        // Desequence the first flight and recompute the sequence
        first.Desequence();
        _scheduler.Schedule(sequence);

        // Sanity check, second flight should no longer have a delay
        second.ScheduledFeederFixTime.ShouldBe(second.ScheduledFeederFixTime);
        second.ScheduledLandingTime.ShouldBe(second.EstimatedLandingTime);

        // Act
        first.Resume();
        _scheduler.Schedule(sequence);

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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(first)
            .WithFlight(second)
            .Build();

        // First pass, normal sequence
        _scheduler.Schedule(sequence);

        // Desequence the first flight and recompute the sequence
        sequence.DesequenceFlight(first.Callsign, _scheduler);

        // Act
        // Stabilize the second flight and resume the first one
        second.SetState(State.Stable);
        sequence.ResumeSequencing(first.Callsign, _scheduler);

        // Assert
        // No delay for the second flight since it was stabilized first
        sequence.Flights.OrderBy(f => f.ScheduledLandingTime).Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
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

        var fixedLandingTime = _clock.UtcNow().AddMinutes(21);
        var fixedFlight = new FlightBuilder("QFA2")
            .WithLandingTime(fixedLandingTime, manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leadingFlight)
            .WithFlight(fixedFlight)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        // Fixed flight should have no change to its schedule
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
        fixedFlight.ScheduledLandingTime.ShouldBe(fixedLandingTime);
        leadingFlight.ScheduledLandingTime.ShouldBe(fixedLandingTime.Add(_landingRate));
    }

    [Fact]
    public void WhenDelayingAFlight_InConflictWithMultipleManualLandingTimeFlights_TheFlightIsFurtherDelayed()
    {
        // Arrange
        var subjectFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var fixedLandingTime1 = _clock.UtcNow().AddMinutes(22);
        var fixedFlight1 = new FlightBuilder("QFA2")
            .WithLandingTime(fixedLandingTime1, manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var fixedLandingTime2 = _clock.UtcNow().AddMinutes(25);
        var fixedFlight2 = new FlightBuilder("QFA3")
            .WithLandingTime(fixedLandingTime2, manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(subjectFlight)
            .WithFlight(fixedFlight1)
            .WithFlight(fixedFlight2)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        // No delay for the first flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA3", "QFA1"]);

        // Fixed flights should have no change to their schedules
        fixedFlight1.ScheduledLandingTime.ShouldBe(fixedLandingTime1);
        fixedFlight2.ScheduledLandingTime.ShouldBe(fixedLandingTime2);

        // Subject flight should be delayed behind the fixed flights since delaying it behind the leading flight
        // puts it in conflict with the fixed flights
        subjectFlight.ScheduledLandingTime.ShouldBe(fixedLandingTime2.Add(_landingRate));
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
