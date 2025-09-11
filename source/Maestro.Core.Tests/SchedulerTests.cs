using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;
using RunwayDependency = Maestro.Core.Configuration.RunwayDependency;

namespace Maestro.Core.Tests;

public class SchedulerTests(
    PerformanceLookupFixture performanceLookupFixture,
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    static TimeSpan _landingRate = TimeSpan.FromSeconds(180);

    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;
    readonly IClock _clock = clockFixture.Instance;
    readonly IScheduler _scheduler = CreateScheduler(performanceLookupFixture, airportConfigurationFixture, clockFixture.Instance);

    static IScheduler CreateScheduler(PerformanceLookupFixture performanceLookupFixture, AirportConfigurationFixture airportConfigurationFixture, IClock clock)
    {
        var performanceLookup = performanceLookupFixture.Instance;
        var runwayAssigner = new RunwayScoreCalculator();
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
            clock,
            Substitute.For<ILogger>());
    }

    [Fact]
    public void WhenSchedulingOvershootFlightBetweenTwoFrozenFlights_ItIsScheduledInBetweenThem()
    {
        // Arrange
        // Create two frozen flights with enough gap for an overshoot flight
        var firstFrozenFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(-2))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var secondFrozenFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow())
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(16))
            .WithLandingTime(_clock.UtcNow().AddMinutes(16))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstFrozenFlight)
            .WithFlight(secondFrozenFlight)
            .Build();

        // Create an overshoot flight with landing landing time between the frozen flights
        var overshootFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(8))
            .WithLandingTime(firstFrozenFlight.LandingTime.Add(_landingRate))
            .WithRunway("34L")
            .WithState(State.Overshoot)
            .Build();

        // Act
        sequence.AddFlight(overshootFlight, _scheduler);

        // Assert
        // Overshoot flight should be scheduled at its landing time
        overshootFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(13));

        // Frozen flights should remain unchanged
        firstFrozenFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(10));
        secondFrozenFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(16));

        // Sequence order should be chronological
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA3", "QFA2"]);
    }

    [Fact]
    public void WhenSchedulingOvershootFlightBetweenTwoFrozenFlightsWithNoSpace_ItIsDelayedUntilThereIsSpace()
    {
        // Arrange
        // Create two frozen flights with no adequate gap (less than landing rate)
        var firstFrozenFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(-2))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var secondFrozenFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow())
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(15))
            .WithLandingTime(_clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstFrozenFlight)
            .WithFlight(secondFrozenFlight)
            .Build();

        // Create an overshoot flight with a landing time between the frozen flights
        var overshootFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(8))
            .WithLandingTime(firstFrozenFlight.LandingTime.Add(_landingRate))
            .WithRunway("34L")
            .WithState(State.Overshoot)
            .Build();

        // Act
        sequence.AddFlight(overshootFlight, _scheduler);

        // Assert
        // Overshoot flight should be delayed behind the second frozen flight
        var expectedTime = secondFrozenFlight.LandingTime.Add(_landingRate);
        overshootFlight.LandingTime.ShouldBe(expectedTime);

        // Frozen flights should remain unchanged
        firstFrozenFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(10));
        secondFrozenFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(15));

        // Sequence order should be chronological by scheduled time
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2", "QFA3"]);
    }

    [Fact]
    public void WhenSchedulingOvershootFlightInFrontOfSuperStableFlights_SuperStableFlightsAreDelayed()
    {
        // Arrange
        // Create SuperStable flights
        var firstSuperStableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(-5)) // Past feeder fix
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var secondSuperStableFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(-2)) // Past feeder fix
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(23))
            .WithLandingTime(_clock.UtcNow().AddMinutes(23))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstSuperStableFlight)
            .WithFlight(secondSuperStableFlight)
            .Build();

        // Create an overshoot flight with target landing time before SuperStable flights
        var overshootFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Overshoot)
            .Build();

        // Act
        sequence.AddFlight(overshootFlight, _scheduler);

        // Assert
        // Overshoot flight should be scheduled at its target time
        overshootFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));

        // SuperStable flights should be delayed to accommodate the overshoot flight
        firstSuperStableFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(23));
        secondSuperStableFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(26));

        // Sequence order should be overshoot, then SuperStable flights
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA3", "QFA1", "QFA2"]);
    }

    [Fact]
    public void WhenSchedulingOvershootFlight_SequenceIsRecalculatedForAllSubsequentFlights()
    {
        // Arrange
        // Create multiple flights in sequence
        var frozenFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(5))
            .WithLandingTime(_clock.UtcNow().AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var superStableFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(8))
            .WithLandingTime(_clock.UtcNow().AddMinutes(8))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var stableFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(11))
            .WithLandingTime(_clock.UtcNow().AddMinutes(11))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("QFA4")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(14))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(14))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(frozenFlight)
            .WithFlight(superStableFlight)
            .WithFlight(stableFlight)
            .WithFlight(unstableFlight)
            .Build();

        // Create an overshoot flight that will be inserted after the frozen flight
        var overshootFlight = new FlightBuilder("QFA5")
            .WithLandingTime(frozenFlight.LandingTime.Add(_landingRate))
            .WithRunway("34L")
            .WithState(State.Overshoot)
            .Build();

        // Act
        sequence.AddFlight(overshootFlight, _scheduler);

        // Assert
        // Frozen flight should remain unchanged
        frozenFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(5));

        // Overshoot flight should be scheduled just after the frozen flight
        overshootFlight.LandingTime.ShouldBe(frozenFlight.LandingTime.Add(_landingRate));

        // SuperStable flight should be delayed due to overshoot insertion
        superStableFlight.LandingTime.ShouldBe(overshootFlight.LandingTime.Add(_landingRate));

        // Stable flight should be recalculated and delayed due to overshoot insertion
        stableFlight.LandingTime.ShouldBe(superStableFlight.LandingTime.Add(_landingRate));

        // Unstable flight should also be recalculated due to stable flight moving
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate));

        // Sequence order should be correct
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA5", "QFA2", "QFA3", "QFA4"]);
    }

    [Fact]
    public void WhenSchedulingASingleFlight_ItIsNotDelayed()
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
        flight.FeederFixTime.ShouldBe(flight.FeederFixEstimate);
        flight.LandingTime.ShouldBe(flight.LandingEstimate);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
        flight.RemainingDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenUnstableFlightWithNewEstimatesEarlierThanStable_UnstableFlightIsDelayed()
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
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        // First pass
        _scheduler.Schedule(sequence);

        // Sanity check
        firstFlight.FeederFixTime.ShouldBe(firstFlight.FeederFixEstimate);
        firstFlight.LandingTime.ShouldBe(firstFlight.LandingEstimate);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        secondFlight.FeederFixTime.ShouldBe(firstFlight.FeederFixTime!.Value.Add(_landingRate));
        secondFlight.LandingTime.ShouldBe(firstFlight.LandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));

        // Stablise the first flight so that it doesn't move for unstable flights
        firstFlight.SetState(State.Stable, _clock);

        // Update the ETA for the second flight so that it should be earlier than the first one
        secondFlight.UpdateFeederFixEstimate(firstFlight.FeederFixEstimate.Value.AddMinutes(-1));
        secondFlight.UpdateLandingEstimate(firstFlight.LandingEstimate.AddMinutes(-1));

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        // The sequence order should not change
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);

        // The stable flight should not change
        firstFlight.FeederFixTime.ShouldBe(firstFlight.FeederFixEstimate);
        firstFlight.LandingTime.ShouldBe(firstFlight.LandingEstimate);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // The unstable should be delayed
        secondFlight.FeederFixTime.ShouldBe(firstFlight.FeederFixTime!.Value.Add(_landingRate));
        secondFlight.LandingTime.ShouldBe(firstFlight.LandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(4));
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public void WhenSchedulingANewFlight_ItIsScheduledAfterFixedFlights(State state)
    {
        // Arrange
        var fixedFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(fixedFlight)
            .Build();

        // New flight added with an earlier ETA
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.New)
            .Build();

        sequence.AddFlight(secondFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);

        fixedFlight.FeederFixTime.ShouldBe(fixedFlight.FeederFixEstimate);
        fixedFlight.LandingTime.ShouldBe(fixedFlight.LandingEstimate);
        fixedFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        secondFlight.FeederFixTime.ShouldBe(fixedFlight.FeederFixTime!.Value.Add(_landingRate));
        secondFlight.LandingTime.ShouldBe(fixedFlight.LandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(4));
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    public void WhenSchedulingANewFlight_StableFlightsAreRecalculated(State state)
    {
        // Arrange
        var firstStableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(25))
            .WithLandingTime(_clock.UtcNow().AddMinutes(25))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var secondStableFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(13))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(28))
            .WithLandingTime(_clock.UtcNow().AddMinutes(28))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstStableFlight)
            .WithFlight(secondStableFlight)
            .Build();

        // Create a new flight with earlier ETA that will displace stable flights
        var newFlight = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(12))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(22)) // Earlier than stable flights
            .WithRunway("34L")
            .WithState(State.New)
            .Build();

        // Act
        sequence.AddFlight(newFlight, _scheduler);

        // Assert
        // New flight should be scheduled at its ETA and become unstable
        newFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(22));
        newFlight.State.ShouldBe(State.Unstable);

        // Stable flights should be recalculated and delayed
        firstStableFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(22).Add(_landingRate));
        secondStableFlight.LandingTime.ShouldBe(firstStableFlight.LandingTime.Add(_landingRate));

        // Sequence order should be new flight first, then stable flights in order
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA3", "QFA1", "QFA2"]);
    }

    [Fact]
    public void WhenSchedulingMultipleFlights_OnDifferentRunways_TheyAreNotSeparated()
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
        first.FeederFixTime.ShouldBe(first.FeederFixEstimate);
        first.LandingTime.ShouldBe(first.LandingEstimate);
        first.TotalDelay.ShouldBe(TimeSpan.Zero);

        second.FeederFixTime.ShouldBe(second.FeederFixEstimate);
        second.LandingTime.ShouldBe(second.LandingEstimate);
        second.TotalDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenSchedulingFlights_WithRunwayDependencies_DependencySeparationIsApplied()
    {
        // Arrange - Create runway configuration with dependencies
        var runway34L = new RunwayConfiguration
        {
            Identifier = "34L",
            LandingRateSeconds = 180, // 3 minute spacing on same runway
            Dependencies =
            [
                new RunwayDependency
                {
                    RunwayIdentifier = "34R",
                    SeparationSeconds = 30 // 30 second separation from 34R
                }
            ]
        };

        var runway34R = new RunwayConfiguration
        {
            Identifier = "34R",
            LandingRateSeconds = 180, // 3 minute spacing on same runway
            Dependencies =
            [
                new RunwayDependency
                {
                    RunwayIdentifier = "34L",
                    SeparationSeconds = 30 // 30 second separation from 34L
                }
            ]
        };

        var runwayMode = new RunwayModeConfiguration
        {
            Identifier = "34LR",
            Runways = [runway34L, runway34R]
        };

        // Create a flight on 34L that will cause conflicts on 34R
        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(runwayMode)
            .WithFlight(leaderFlight)
            .Build();

        // Create a follower flight on 34R with ETA within the dependency separation window
        var followerFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(leaderFlight.LandingTime) // Landing at the same time as the leader
            .WithRunway("34R")
            .WithState(State.Unstable)
            .Build();

        // Act
        sequence.AddFlight(followerFlight, _scheduler);

        // Assert
        // Leader flight should remain unchanged
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);
        leaderFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Follower flight should be delayed by the dependency separation (30 seconds)
        var expectedLandingTime = leaderFlight.LandingTime.AddSeconds(30);
        followerFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        followerFlight.LandingTime.ShouldBe(expectedLandingTime);

        // Sequence order should be leader first, then follower
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);
    }

    [Fact]
    public void WhenSchedulingFlights_WithRunwayDependencies_AndNoConflict_NoSeparationApplied()
    {
        // Arrange - Create runway configuration with dependencies
        var runway34L = new RunwayConfiguration
        {
            Identifier = "34L",
            LandingRateSeconds = 180,
            Dependencies =
            [
                new RunwayDependency
                {
                    RunwayIdentifier = "34R",
                    SeparationSeconds = 30
                }
            ]
        };

        var runway34R = new RunwayConfiguration
        {
            Identifier = "34R",
            LandingRateSeconds = 180,
            Dependencies = []
        };

        var runwayMode = new RunwayModeConfiguration
        {
            Identifier = "Dual",
            Runways = [runway34L, runway34R]
        };

        // Create flights with sufficient separation (more than dependency requirement)
        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34R")
            .WithState(State.Stable)
            .Build();

        var followerFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddSeconds(45)) // 45 seconds after leader (more than 30 second dependency)
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(runwayMode)
            .WithFlight(leaderFlight)
            .WithFlight(followerFlight)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

        // Assert - No delays should be applied since there's sufficient separation
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);
        leaderFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        followerFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        followerFlight.LandingTime.ShouldBe(followerFlight.LandingEstimate);
        followerFlight.TotalDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenSchedulingMultipleFlights_AndNoConflictExists_NoDelaysAreApplied()
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

        farAwayFlight.FeederFixTime.ShouldBe(farAwayFlight.FeederFixEstimate);
        farAwayFlight.LandingTime.ShouldBe(farAwayFlight.LandingEstimate);

        closeFlight.FeederFixTime.ShouldBe(closeFlight.FeederFixEstimate);
        closeFlight.LandingTime.ShouldBe(closeFlight.LandingEstimate);

        veryCloseFlight.FeederFixTime.ShouldBe(veryCloseFlight.FeederFixEstimate);
        veryCloseFlight.LandingTime.ShouldBe(veryCloseFlight.LandingEstimate);
    }

    [Fact]// TODO: This should move to the estimate calculator
    public void WhenReroutedToAnotherFix_EstimatesAreStillCalculatedToFeederFix()
    {
        // Arrange - Create a flight with initial feeder fix and estimates
        var originalFeederFix = "RIVET";
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix(originalFeederFix)
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        // First pass - schedule with original feeder fix
        _scheduler.Schedule(sequence);

        // Verify initial state
        flight.FeederFixIdentifier.ShouldBe(originalFeederFix);
        flight.FeederFixTime.ShouldBe(flight.FeederFixEstimate);
        flight.LandingTime.ShouldBe(flight.LandingEstimate);

        // Act - Reroute flight to a different feeder fix but keep the same trajectory
        var originalEstimatedFeederFixTime = flight.FeederFixEstimate!.Value;
        var originalEstimatedLandingTime = flight.LandingEstimate;

        var newFeederFix = "WELSH";
        flight.SetFeederFix(newFeederFix, originalEstimatedFeederFixTime);

        // Re-schedule after rerouting
        _scheduler.Schedule(sequence);

        // Assert - Estimates should remain the same even though feeder fix changed
        flight.FeederFixIdentifier.ShouldBe(newFeederFix);
        flight.FeederFixEstimate.ShouldBe(originalEstimatedFeederFixTime);
        flight.FeederFixTime.ShouldBe(originalEstimatedFeederFixTime);
        flight.LandingEstimate.ShouldBe(originalEstimatedLandingTime);
        flight.LandingTime.ShouldBe(originalEstimatedLandingTime);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public void WhenAFlightHasStablised_ItsPositionInSequenceIsNotRecomputed(State state)
    {
        // Arrange - Create multiple flights with separate ETAs
        var firstFlight = new FlightBuilder("QFA1")
            .WithActivationTime(_clock.UtcNow().AddMinutes(-30))
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(-6))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(16))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithActivationTime(_clock.UtcNow().AddMinutes(-30))
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(-3))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var thirdFlight = new FlightBuilder("QFA3")
            .WithActivationTime(_clock.UtcNow().AddMinutes(-30))
            .WithFeederFixEstimate(_clock.UtcNow())
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(22))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .WithFlight(thirdFlight)
            .Build();

        // Initial scheduling
        _scheduler.Schedule(sequence);

        // Record initial order and times
        var initialOrder = sequence.Flights.Order().Select(f => f.Callsign).ToArray();
        initialOrder.ShouldBe(["QFA1", "QFA2", "QFA3"]);

        // Act - Jumble the ETAs (leap frog)
        secondFlight.UpdateLandingEstimate(_clock.UtcNow().AddMinutes(16));
        thirdFlight.UpdateLandingEstimate(_clock.UtcNow().AddMinutes(16));

        // Re-schedule after updating ETAs
        _scheduler.Schedule(sequence);

        // Assert - Position in sequence should remain unchanged despite ETA changes
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2", "QFA3"]);

        // However, delays should be updated to maintain the original sequence order
        // First flight should not be delayed (keeps its ETA)
        firstFlight.LandingTime.ShouldBe(firstFlight.LandingEstimate);
        secondFlight.LandingTime.ShouldBe(firstFlight.LandingTime.Add(_landingRate));
        thirdFlight.LandingTime.ShouldBe(secondFlight.LandingTime.Add(_landingRate));
    }

    [Theory]
    [InlineData(State.Pending)]
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
        first.FeederFixTime.ShouldBe(first.FeederFixEstimate);
        first.LandingTime.ShouldBe(first.LandingEstimate);

        // Second result should now be delayed
        second.FeederFixTime.ShouldBe(first.FeederFixTime.Value.Add(_landingRate));
        second.LandingTime.ShouldBe(first.LandingTime.Add(_landingRate));

        // Act
        if (state == State.Desequenced)
        {
            sequence.DesequenceFlight(first.Callsign, _scheduler);
        }
        else if (state == State.Removed)
        {
            sequence.RemoveFlight(first.Callsign, _scheduler);
        }
        else if (state == State.Pending)
        {
            first.MakePending();
            _scheduler.Schedule(sequence);
        }

        // Assert

        // Second flight shouldn't have any delay
        second.FeederFixTime.ShouldBe(second.FeederFixTime);
        second.LandingTime.ShouldBe(second.LandingEstimate);
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
        // Fixed flight should have no change to its landing time
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
        fixedFlight.LandingTime.ShouldBe(fixedLandingTime);
        leadingFlight.LandingTime.ShouldBe(fixedLandingTime.Add(_landingRate));
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
        fixedFlight1.LandingTime.ShouldBe(fixedLandingTime1);
        fixedFlight2.LandingTime.ShouldBe(fixedLandingTime2);

        // Subject flight should be delayed behind the fixed flights since delaying it behind the leading flight
        // puts it in conflict with the fixed flights
        subjectFlight.LandingTime.ShouldBe(fixedLandingTime2.Add(_landingRate));
    }

    [Theory]
    [InlineData(true)]  // NoDelay flight
    [InlineData(false)] // Manual landing time flight
    public void WhenFlightHasNoDelayOrManualLandingTime_FlightIsNotDelayed(bool useNoDelay)
    {
        // Arrange - Create a leader flight
        var leaderFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create follower flight with either NoDelay or ManualLandingTime
        var followerBuilder = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10).AddSeconds(30))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddSeconds(30))
            .WithRunway("34L")
            .WithState(State.New);

        if (useNoDelay)
        {
            followerBuilder.NoDelay();
        }
        else
        {
            followerBuilder.WithLandingTime(_clock.UtcNow().AddMinutes(20).AddSeconds(30), manual: true);
        }

        var followerFlight = followerBuilder.Build();
        sequence.AddFlight(followerFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - The follower flight should not be delayed despite conflict
        followerFlight.State.ShouldBe(State.Unstable);
        followerFlight.LandingTime.ShouldBe(followerFlight.LandingEstimate);
        followerFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        if (useNoDelay)
        {
            followerFlight.NoDelay.ShouldBeTrue();
        }
        else
        {
            followerFlight.ManualLandingTime.ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(true)]  // NoDelay flight
    [InlineData(false)] // Manual landing time flight
    public void WhenNoDelayOrManualFlightConflictsWithLeader_LeaderIsDelayed(bool useNoDelay)
    {
        // Arrange - Create a leader flight (Stable, can be moved)
        var leaderFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a NoDelay/Manual flight with ETA just before the leader
        var priorityBuilder = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.New);

        if (useNoDelay)
        {
            priorityBuilder.NoDelay();
        }
        else
        {
            priorityBuilder.WithLandingTime(_clock.UtcNow().AddMinutes(20), manual: true);
        }

        var priorityFlight = priorityBuilder.Build();
        sequence.AddFlight(priorityFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - The priority flight should not be delayed
        priorityFlight.LandingTime.ShouldBe(priorityFlight.LandingEstimate);

        // The leader flight should be delayed by the landing rate
        var expectedDelayedTime = priorityFlight.LandingEstimate.Add(_landingRate);
        leaderFlight.LandingTime.ShouldBe(expectedDelayedTime);

        // Sequence order should be priority flight first, then leader
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
    }

    [Theory]
    [InlineData(true, true)]   // Both NoDelay
    [InlineData(true, false)]  // Leader NoDelay, follower Manual
    [InlineData(false, true)]  // Leader Manual, follower NoDelay
    [InlineData(false, false)] // Both Manual landing time
    public void WhenBothFlightsHaveNoDelayOrManual_NoDelayAppliedToEither(bool leaderNoDelay, bool followerNoDelay)
    {
        // Arrange - Create a leader flight with NoDelay or Manual landing time
        var leaderBuilder = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable);

        if (leaderNoDelay)
        {
            leaderBuilder.NoDelay();
        }
        else
        {
            leaderBuilder.WithLandingTime(_clock.UtcNow().AddMinutes(20), manual: true);
        }

        var leaderFlight = leaderBuilder.Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a follower flight also with NoDelay or Manual landing time, ETA too close to leader
        var followerBuilder = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10).AddSeconds(30))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddSeconds(30))
            .WithRunway("34L")
            .WithState(State.New);

        if (followerNoDelay)
        {
            followerBuilder.NoDelay();
        }
        else
        {
            followerBuilder.WithLandingTime(_clock.UtcNow().AddMinutes(20).AddSeconds(30), manual: true);
        }

        var followerFlight = followerBuilder.Build();

        // Act
        sequence.AddFlight(followerFlight, _scheduler);

        // Assert - Neither flight should be delayed
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);
        leaderFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        followerFlight.State.ShouldBe(State.Unstable);
        followerFlight.LandingTime.ShouldBe(followerFlight.LandingEstimate);
        followerFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Verify the flags are set correctly
        if (leaderNoDelay)
        {
            leaderFlight.NoDelay.ShouldBeTrue();
        }
        else
        {
            leaderFlight.ManualLandingTime.ShouldBeTrue();
        }

        if (followerNoDelay)
        {
            followerFlight.NoDelay.ShouldBeTrue();
        }
        else
        {
            followerFlight.ManualLandingTime.ShouldBeTrue();
        }
    }

    [Fact]
    public void WhenStableFlightHasManualLandingTimeAndUnstableFlightHasEarlierEstimateWithNoConflict_EarlierFlightIsNotDelayed()
    {
        // Arrange - Create a stable flight with manual landing time
        var stableFlight = new FlightBuilder("QFA1")
            .WithLandingTime(_clock.UtcNow().AddMinutes(30), manual: true) // Manual time far in future
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(stableFlight)
            .Build();

        // Schedule the stable flight first
        _scheduler.Schedule(sequence);

        // Create an unstable flight with earlier estimate but no conflict (more than landing rate separation)
        var unstableFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20)) // 10 minutes before stable flight
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.AddFlight(unstableFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - The unstable flight should not be delayed since there's no conflict
        unstableFlight.State.ShouldBe(State.Unstable);
        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate);
        unstableFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Stable flight should keep its manual time
        stableFlight.ManualLandingTime.ShouldBeTrue();
        stableFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(30));

        // Sequence order should be unstable flight first (earlier time), then stable flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
    }

    // TODO: Double check priority flight behaviour
    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void PriorityFlights_ArePrioritised(State leaderState)
    {
        // Arrange - Create a leader flight
        var leaderFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(leaderState)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a priority flight with ETA too close to the leader (within landing rate)
        var priorityFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10).AddMinutes(1))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddMinutes(1))
            .WithRunway("34L")
            .HighPriority()
            .WithState(State.New)
            .Build();

        // Act
        sequence.AddFlight(priorityFlight, _scheduler);

        // Assert - Priority flight gets priority over other New/Unstable flights but not over Stable flights
        priorityFlight.State.ShouldBe(State.Unstable);
        priorityFlight.HighPriority.ShouldBeTrue();

        // HighPriority flight shouldn't be delayed before other flights
        priorityFlight.LandingTime.ShouldBe(priorityFlight.LandingEstimate);
        priorityFlight.AssignedRunwayIdentifier.ShouldBe("34L");

        // Leader (Stable flight) should keep its original time
        leaderFlight.LandingTime.ShouldBe(priorityFlight.LandingTime.Add(_landingRate));
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");
    }

    [Fact]
    public void PriorityFlights_AreNotPrioritisedOverFrozenFlights()
    {
        // Arrange - Create a leader flight
        var frozenFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(frozenFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a priority flight with ETA too close to the leader (within landing rate)
        var priorityFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10).AddMinutes(1))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddMinutes(1))
            .WithRunway("34L")
            .HighPriority()
            .WithState(State.New)
            .Build();

        // Act
        sequence.AddFlight(priorityFlight, _scheduler);

        // Assert - Priority flight gets priority over other New/Unstable flights but not over Stable flights
        priorityFlight.State.ShouldBe(State.Unstable);
        priorityFlight.HighPriority.ShouldBeTrue();

        // HighPriority flights should be delayed behind Frozen flights
        priorityFlight.LandingTime.ShouldBe(frozenFlight.LandingTime.Add(_landingRate));
        priorityFlight.AssignedRunwayIdentifier.ShouldBe("34L");

        // Leader (Frozen flight) should keep its original time
        frozenFlight.LandingTime.ShouldBe(frozenFlight.LandingEstimate);
        frozenFlight.AssignedRunwayIdentifier.ShouldBe("34L");
    }

    [Fact]
    public void WhenTwoFlightsShareAnETA_HighPriorityFlightWins()
    {
        // Arrange - Create two flights with the same ETA, one high priority
        var regularFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.New)
            .Build();

        var priorityFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20)) // Same ETA as regular flight
            .WithRunway("34L")
            .HighPriority()
            .WithState(State.New)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(regularFlight)
            .WithFlight(priorityFlight)
            .Build();

        // Act
        _scheduler.Schedule(sequence);

        // Assert - Priority flight should be scheduled first despite same ETA
        priorityFlight.HighPriority.ShouldBeTrue();

        // Priority flight should get the better slot (its ETA)
        priorityFlight.LandingTime.ShouldBe(priorityFlight.LandingEstimate);
        priorityFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Regular flight should be delayed by landing rate
        var expectedDelayedTime = priorityFlight.LandingEstimate.Add(_landingRate);
        regularFlight.LandingTime.ShouldBe(expectedDelayedTime);
        regularFlight.TotalDelay.ShouldBe(_landingRate);

        // Sequence order should be priority flight first, then regular flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
    }

    [Fact(Skip="Incorrect behaviour, needs review")]
    public void WhenHighPriorityFlightHasEarlierETAThanStableFlight_HighPriorityFlightIsDelayed()
    {
        // Arrange - Create a stable flight first
        var stableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(stableFlight)
            .Build();

        // Schedule the stable flight first
        _scheduler.Schedule(sequence);

        // Create a high priority flight with an earlier ETA
        var priorityFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(8))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(18)) // 2 minutes earlier than stable flight
            .WithRunway("34L")
            .HighPriority()
            .WithState(State.New)
            .Build();

        sequence.AddFlight(priorityFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - High priority flight should get its ETA and push back the stable flight
        priorityFlight.State.ShouldBe(State.Unstable);
        priorityFlight.HighPriority.ShouldBeTrue();

        // Priority flight should still be delayed
        priorityFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate));

        // Stable flight should be delayed to accommodate the priority flight
        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate);

        // Sequence order should be priority flight first, then stable flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);
    }

    [Fact]
    public void WhenAFlightHasZeroDelay_NoDelayIsApplied()
    {
        // Arrange - Create a leader flight
        var leaderFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a zero delay (NoDelay) flight with ETA too close to the leader (within landing rate)
        var zeroDelayFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10).AddSeconds(30))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddSeconds(30))
            .WithRunway("34L")
            .NoDelay() // Zero delay is implemented as NoDelay flag
            .WithState(State.New)
            .Build();

        // Act
        sequence.AddFlight(zeroDelayFlight, _scheduler);

        // Assert - Zero delay flight should not be delayed despite conflict
        zeroDelayFlight.State.ShouldBe(State.Unstable);
        zeroDelayFlight.NoDelay.ShouldBeTrue();
        zeroDelayFlight.LandingTime.ShouldBe(zeroDelayFlight.LandingEstimate);
        zeroDelayFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // The leader flight should be delayed by the landing rate to avoid conflict
        var expectedDelayedTime = zeroDelayFlight.LandingEstimate.Add(_landingRate);
        leaderFlight.LandingTime.ShouldBe(expectedDelayedTime);

        // Sequence order should be zero delay flight first, then leader
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);
    }

    [Fact]
    public void WhenMultipleRunwaysAreAvailable_AndAnotherRunwayResultsInLessDelay_FlightAssignedToOtherRunway()
    {
        // Arrange - Create a leader flight on 34L
        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Add a second flight with ETA too close to the leader (within landing rate)
        var followerFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        // Act
        sequence.AddFlight(followerFlight, _scheduler);

        // Assert - The follower should be assigned to 34R to avoid delay
        followerFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        followerFlight.LandingTime.ShouldBe(followerFlight.LandingEstimate);

        // Leader should be unaffected
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);
    }

    [Fact]
    public void WhenAllRunwaysHaveDelay_FlightAssignedToRunwayWithLeastDelay()
    {
        // Arrange - Create leader flights on both runways
        var leader34R = new FlightBuilder("QFA1")
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.SuperStable) // Use SuperStable so it can't be moved
            .Build();

        var leader34L = new FlightBuilder("QFA2")
            .WithLandingTime(_clock.UtcNow().AddMinutes(8))
            .WithRunway("34R")
            .WithState(State.SuperStable) // Use SuperStable so it can't be moved
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(leader34R)
            .WithFlight(leader34L)
            .Build();

        // Add a new flight that would conflict with both leaders (ETA between both leaders)
        var newFlight = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(7))
            .WithRunway("34L") // Assign 34L initially
            .WithState(State.New)
            .Build();

        // Act
        sequence.AddFlight(newFlight, _scheduler);

        // Assert
        // Since 34R creates less delay (4 min vs 6 min), it should be chosen
        newFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        newFlight.LandingTime.ShouldBe(leader34L.LandingTime.Add(_landingRate));
        newFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(4));
    }

    [Fact]
    public void WhenNoDelayFlightConflictsAndOtherRunwayAvailable_LeaderMovedToOtherRunway()
    {
        // Arrange - Create a leader flight on 34L
        var leaderFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a NoDelay flight with ETA just before the leader
        var noDelayFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9).AddSeconds(30))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19).AddSeconds(30))
            .WithRunway("34L")
            .NoDelay()
            .WithState(State.New)
            .Build();

        sequence.AddFlight(noDelayFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - The NoDelay flight should keep its ETA and runway
        noDelayFlight.State.ShouldBe(State.Unstable);
        noDelayFlight.LandingTime.ShouldBe(noDelayFlight.LandingEstimate);
        noDelayFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        noDelayFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // The leader flight should ideally be moved to 34R without delay instead of being delayed on 34L
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);
        leaderFlight.TotalDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenFlightDelayedBeyondRunwayModeChange_NewRunwayModeUsed()
    {
        // Arrange
        var initialRunwayMode = new RunwayModeConfiguration
        {
            Identifier = "34L",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = 180 // 3 minute spacing
                }
            ]
        };

        // Create future runway mode with different runway and different landing rate
        var futureRunwayMode = new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = 120 // 2 minute spacing
                }
            ]
        };

        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(initialRunwayMode)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule a runway mode change for 20 minutes from now (after the leader lands)
        var modeChangeTime = _clock.UtcNow().AddMinutes(20);
        sequence.ChangeRunwayMode(new RunwayMode(futureRunwayMode), modeChangeTime, modeChangeTime, _scheduler);

        // Add a flight that would conflict with the leader, forcing it to be delayed beyond the mode change
        var followerFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithState(State.Unstable)
            .Build();

        sequence.AddFlight(followerFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        // Leader should remain on original runway with original schedule
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);

        // The follower flight should be scheduled using the new runway mode with 2-minute spacing
        followerFlight.State.ShouldBe(State.Unstable);
        followerFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        followerFlight.LandingTime.ShouldBe(leaderFlight.LandingTime.AddSeconds(120));
    }

    [Fact]
    public void WhenFlightDelayedInBetweenRunwayModeChange_NewRunwayModeUsed()
    {
        // Arrange
        var initialRunwayMode = new RunwayModeConfiguration
        {
            Identifier = "34L",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = 180 // 3 minute spacing
                }
            ]
        };

        // Create future runway mode with different runway and different landing rate
        var futureRunwayMode = new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = 120 // 2 minute spacing
                }
            ]
        };

        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(initialRunwayMode)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule a runway mode change with a break-period of 10 minutes
        var lastLandingTime = _clock.UtcNow().AddMinutes(20);
        var firstLandingTime = _clock.UtcNow().AddMinutes(30);
        sequence.ChangeRunwayMode(new RunwayMode(futureRunwayMode), lastLandingTime, firstLandingTime, _scheduler);

        // Add a flight that would conflict with the leader, forcing it to be delayed beyond the mode change
        var followerFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithState(State.Unstable)
            .Build();

        // Act
        sequence.AddFlight(followerFlight, _scheduler);

        // Assert
        // Leader should remain on original runway with original schedule
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);

        // The follower flight should be scheduled using the new runway with the first landing time
        followerFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        followerFlight.LandingTime.ShouldBe(firstLandingTime);
    }

    [Fact]
    public void WhenAFlightLands_AfterRunwayChange_TheyAreScheduledForTheNextRunway()
    {
        // Arrange
        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var trailerFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .WithFlight(trailerFlight)
            .Build();

        // Act
        sequence.ChangeRunwayMode(
            new RunwayMode(
                new RunwayModeConfiguration
                {
                    Identifier = "34R",
                    Runways =
                    [
                        new RunwayConfiguration
                        {
                            Identifier = "34R",
                            LandingRateSeconds = (int)_landingRate.TotalSeconds
                        }
                    ]
                }),
            _clock.UtcNow().AddMinutes(10),
            _clock.UtcNow().AddMinutes(15),
            _scheduler);

        // Assert
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");

        trailerFlight.LandingTime.ShouldBe(trailerFlight.LandingEstimate);
        trailerFlight.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public void WhenFlightHasManualRunwayAssignment_ItIsNotReassignedToDifferentRunway()
    {
        // Arrange - Create a leader flight on 34L
        var leaderFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(leaderFlight)
            .Build();

        // Schedule the leader first
        _scheduler.Schedule(sequence);

        // Create a follower flight with manual runway assignment to 34L
        var manualRunwayFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10).AddSeconds(30))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20).AddSeconds(30))
            .WithRunway("34L", manual: true) // Manual runway assignment
            .WithState(State.Unstable)
            .Build();

        sequence.AddFlight(manualRunwayFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - The flight should stay on the manually assigned runway (34L) despite delay
        manualRunwayFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        manualRunwayFlight.RunwayManuallyAssigned.ShouldBeTrue();

        // Flight should be delayed rather than moved to 34R
        var expectedDelayedTime = leaderFlight.LandingEstimate.Add(_landingRate);
        manualRunwayFlight.LandingTime.ShouldBe(expectedDelayedTime);

        // Leader should be unaffected
        leaderFlight.AssignedRunwayIdentifier.ShouldBe("34L");
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);

        // Sequence order should be leader first, then manual runway flight
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);
    }

    [Fact]
    public void WhenFlightDelayedIntoSlot_FlightIsFurtherDelayedToEndOfSlot()
    {
        // Arrange
        var leaderFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leaderFlight)
            .Build();

        // Act
        // Create a slot
        var slotStart = _clock.UtcNow().AddMinutes(12);
        var slotEnd = _clock.UtcNow().AddMinutes(20);

        sequence.CreateSlot(slotStart, slotEnd, ["34L"], _scheduler);

        // Not in the slot, but will be delayed into it
        var subject1 = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(11))
            .Build();

        // In the slot
        var subject2 = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(15))
            .Build();

        sequence.AddFlight(subject1, _scheduler);
        sequence.AddFlight(subject2, _scheduler);

        // Assert
        // Leader should remain on original runway with original schedule
        leaderFlight.LandingTime.ShouldBe(leaderFlight.LandingEstimate);

        // Subject1 should be delayed to the end of the slot
        subject1.LandingTime.ShouldBe(slotEnd);

        // Subject2 should be delayed behind subject1, at the end of the slot
        subject2.LandingTime.ShouldBe(subject1.LandingTime.Add(_landingRate));
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void WhenASlotIsAdded_FlightsInTheSlotAreDelayed(State state)
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(flight)
            .Build();

        // Initial scheduling - flight should not be delayed
        _scheduler.Schedule(sequence);
        flight.LandingTime.ShouldBe(flight.LandingEstimate);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Act - Create a slot that conflicts with the flight
        var slotStart = _clock.UtcNow().AddMinutes(10);
        var slotEnd = _clock.UtcNow().AddMinutes(20);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"], _scheduler);

        // Assert - Flight should be delayed to the end of the slot
        flight.LandingTime.ShouldBe(slotEnd);
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void WhenASlotIsAdded_AndAnotherRunwayIsAvailable_FlightsAreMovedToTheOtherRunway()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        // Initial scheduling - flight should not be delayed
        _scheduler.Schedule(sequence);
        flight.LandingTime.ShouldBe(flight.LandingEstimate);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
        flight.AssignedRunwayIdentifier.ShouldBe("34L");

        // Act - Create a slot that conflicts with the flight
        var slotStart = _clock.UtcNow().AddMinutes(5);
        var slotEnd = _clock.UtcNow().AddMinutes(15);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"], _scheduler);

        // Assert - Flight should not be delayed on 34L, but moved to 34
        flight.LandingTime.ShouldBe(flight.LandingEstimate);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public void WhenASlotIsModified_FlightsAreMovedAroundTheSlot()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(25))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        // Create a slot that conflicts with the first flight
        var slotStart = _clock.UtcNow().AddMinutes(10);
        var slotEnd = _clock.UtcNow().AddMinutes(20);
        var slot = sequence.CreateSlot(slotStart, slotEnd, ["34L"], _scheduler);

        // Verify initial state - first flight delayed to end of slot, second flight separated after first
        firstFlight.LandingTime.ShouldBe(slotEnd);
        secondFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(25));

        // Act - Modify the slot to conflict with just the second flight instead
        var newSlotStart = _clock.UtcNow().AddMinutes(22);
        var newSlotEnd = _clock.UtcNow().AddMinutes(30);
        sequence.ModifySlot(slot.Id, newSlotStart, newSlotEnd, _scheduler);

        // Assert - First flight should have no delay, second flight delayed to end of modified slot
        firstFlight.LandingTime.ShouldBe(firstFlight.LandingEstimate);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);
        secondFlight.LandingTime.ShouldBe(newSlotEnd);
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void WhenASlotIsRemoved_DelaysFromTheSlotAreRemoved()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(flight)
            .Build();

        // Create a slot that conflicts with the flight
        var slotStart = _clock.UtcNow().AddMinutes(10);
        var slotEnd = _clock.UtcNow().AddMinutes(20);
        var slot = sequence.CreateSlot(slotStart, slotEnd, ["34L"], _scheduler);

        // Verify flight is delayed due to the slot
        flight.LandingTime.ShouldBe(slotEnd);
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));

        // Act - Delete the slot
        sequence.DeleteSlot(slot.Id, _scheduler);

        // Assert - Flight should no longer be delayed
        flight.LandingTime.ShouldBe(flight.LandingEstimate);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WhenHighPriorityFlightIsBehindUnstableFlightWithNoConflict_NoDelaysAreApplied()
    {
        // Arrange - Create an unstable flight with early ETA
        var unstableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(unstableFlight)
            .Build();

        // Schedule the unstable flight first
        _scheduler.Schedule(sequence);

        // Create a high priority flight with later ETA (no conflict, more than landing rate separation)
        var priorityFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(15))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(25)) // 5 minutes after unstable flight
            .WithRunway("34L")
            .HighPriority()
            .WithState(State.New)
            .Build();

        sequence.AddFlight(priorityFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert - Neither flight should be delayed since there's no conflict
        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate);
        unstableFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        priorityFlight.State.ShouldBe(State.Unstable);
        priorityFlight.HighPriority.ShouldBeTrue();
        priorityFlight.LandingTime.ShouldBe(priorityFlight.LandingEstimate);
        priorityFlight.TotalDelay.ShouldBe(TimeSpan.Zero);

        // Sequence order should be based on landing times (unstable first, then priority)
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2"]);
    }

    [Fact]
    public void WhenFlightDelayedBehindLeaderConflictsWithAnotherFlight_FlightIsDelayedBehindAllConflicts()
    {
        // Arrange - Create two fixed flights 5 minutes apart (landing rate is 3 minutes)
        var firstFixedFlight = new FlightBuilder("QFA1")
            .WithLandingTime(_clock.UtcNow().AddMinutes(20), manual: true)
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var secondFixedFlight = new FlightBuilder("QFA2")
            .WithLandingTime(_clock.UtcNow().AddMinutes(25), manual: true) // 5 minutes after first
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate) // 3 minute landing rate
            .WithFlight(firstFixedFlight)
            .WithFlight(secondFixedFlight)
            .Build();

        // Schedule the fixed flights first
        _scheduler.Schedule(sequence);

        // Create an unstable flight 2 minutes behind the first fixed flight
        var unstableFlight = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(12))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(22)) // 2 minutes after first fixed flight
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.AddFlight(unstableFlight, _scheduler);

        // Act
        _scheduler.Schedule(sequence);

        // Assert
        // Fixed flights should keep their manual times
        firstFixedFlight.ManualLandingTime.ShouldBeTrue();
        firstFixedFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));

        secondFixedFlight.ManualLandingTime.ShouldBeTrue();
        secondFixedFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(25));

        // Unstable flight should be delayed behind both fixed flights
        // Since it can't fit after first (2 min gap < 3 min landing rate) and can't fit before second,
        // it must be delayed to after the second fixed flight
        var expectedDelayedTime = secondFixedFlight.LandingTime.Add(_landingRate);
        unstableFlight.LandingTime.ShouldBe(expectedDelayedTime);
        unstableFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(6)); // 28 - 22 = 6 minutes

        // Sequence order should be first fixed, second fixed, then unstable
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA1", "QFA2", "QFA3"]);
    }

    [Theory]
    [InlineData(State.Unstable, 5)]
    [InlineData(State.Unstable, -5)]
    [InlineData(State.Stable, 5)]
    [InlineData(State.Stable, -5)]
    [InlineData(State.SuperStable, 5)]
    [InlineData(State.SuperStable, -5)]
    [InlineData(State.Frozen, 5)]
    [InlineData(State.Frozen, -5)]
    public void WhenRecomputingFlightThatHasChangedEstimate_FlightMovedToNewEstimateLandingTime(
        State state,
        int minutesDiff)
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(flight)
            .Build();

        // Initial schedule
        _scheduler.Schedule(sequence);
        flight.LandingTime.ShouldBe(flight.LandingEstimate);

        // Act
        var newEstimate = flight.LandingEstimate.AddMinutes(minutesDiff);
        flight.UpdateLandingEstimate(newEstimate);
        _scheduler.Recompute(flight, sequence);

        // Assert
        flight.LandingTime.ShouldBe(newEstimate);
    }

    [Fact]
    public void WhenRecomputingFlightWithTrailingFlights_TrailingFlightsAreRescheduled()
    {
        // Arrange
        var leader = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var subject = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(18)) // ETA 2 minutes ahead of leader
            .WithLandingTime(leader.LandingTime.Add(_landingRate)) // Currently delayed behind leader
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var trailer = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(25))
            .WithLandingTime(subject.LandingTime.Add(_landingRate))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leader)
            .WithFlight(subject)
            .WithFlight(trailer)
            .Build();

        // Act
        _scheduler.Recompute(subject, sequence);

        // Assert

        // Subject should now be in front
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1", "QFA3"]);

        // Subject should have no delay
        subject.LandingTime.ShouldBe(subject.LandingEstimate);

        // Leader should be delayed behind subject
        leader.LandingTime.ShouldBe(subject.LandingTime.Add(_landingRate));

        // Trailer should no longer be delayed
        trailer.LandingTime.ShouldBe(trailer.LandingEstimate);
    }

    [Fact(Skip = "Potentially incorrect behaviour, needs review")]
    public void WhenFlightsAreAddedOutOfChronologicalOrder_FlightsAreSeparatedFromChronologicalLeaderAndTrailer()
    {
        // Arrange
        // No delay for the leader
        var leader = new FlightBuilder("QFA1")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        // Trailer flight leaves enough space for a flight in the middle
        var trailer = new FlightBuilder("QFA3")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(18))
            .WithLandingTime(_clock.UtcNow().AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithSingleRunway("34L", _landingRate)
            .WithFlight(leader)
            .WithFlight(trailer)
            .Build();

        // Act

        // Add a middle flight in between the leader and trailer
        var middle = new FlightBuilder("QFA2")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(12))
            .WithRunway("34L")
            .WithState(State.New)
            .Build();

        sequence.AddFlight(middle, _scheduler);

        // Add another flight _just_ behind the trailer
        // This flight should be separated from the trailer and not the middle flight
        var subject = new FlightBuilder("QFA4")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.New)
            .Build();

        sequence.AddFlight(subject, _scheduler);

        // Assert
        sequence.Flights.OrderBy(f => f.LandingTime).Select(f => f.Callsign).ShouldBe(["QFA1", "QFA2", "QFA3", "QFA4"]);

        // Middle flight should be delayed behind the leader
        leader.LandingTime.ShouldBe(leader.LandingEstimate, "no delay should be applied to the leader");
        middle.LandingTime.ShouldBe(leader.LandingTime.Add(_landingRate), "middle flight should be delayed behind the leader");

        // New flight should be delayed behind the last chronological flight
        subject.LandingTime.ShouldBe(trailer.LandingTime.Add(_landingRate), "last flight should be delayed behind the chronological trailer and not the last flight added to the sequence");
    }
}
