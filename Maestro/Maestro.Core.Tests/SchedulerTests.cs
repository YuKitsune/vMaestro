using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
        lookup.GetPerformanceDataFor(Arg.Any<string>()).Returns(new AircraftPerformanceData { IsJet = true, WakeCategory = WakeCategory.Medium });

        _scheduler = new Scheduler(lookup, new Logger<Scheduler>(NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task SingleFlight_IsNotDelayed()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        await sequence.Add(flight, CancellationToken.None);
        
        // Act
        _scheduler.Schedule(sequence, flight);
        
        // Assert
        flight.ScheduledFeederFixTime.ShouldBe(flight.EstimatedFeederFixTime);
        flight.ScheduledLandingTime.ShouldBe(flight.EstimatedLandingTime);
        flight.TotalDelay.ShouldBe(TimeSpan.Zero);
        flight.RemainingDelay.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task SingleFlight_DuringBlockout_IsDelayed()
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
        await sequence.AddBlockout(blockout, CancellationToken.None);
        await sequence.Add(flight, CancellationToken.None);
        
        // Act
        _scheduler.Schedule(sequence, flight);
        
        // Assert
        flight.ScheduledFeederFixTime.ShouldBe(endTime.AddMinutes(-10));
        flight.ScheduledLandingTime.ShouldBe(endTime);
        flight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task MultipleFlights_DuringBlockout_AreDelayed()
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
        await sequence.AddBlockout(blockout, CancellationToken.None);
        await sequence.Add(firstFlight, CancellationToken.None);
        await sequence.Add(secondFlight, CancellationToken.None);
        
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

        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(3).Add(_landingRate));
    }

    [Fact]
    public async Task NewFlight_EarlierThanStable_StableFlightIsDelayed()
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
        await sequence.Add(stableFlight, CancellationToken.None);
        
        // First pass
        _scheduler.Schedule(sequence, stableFlight);
        
        // New flight added with an earlier ETA
        var unstableFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(9))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();
        
        await sequence.Add(unstableFlight, CancellationToken.None);
        
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

        stableFlight.ScheduledFeederFixTime.ShouldBe(unstableFlight.ScheduledFeederFixTime.Value.Add(_landingRate));
        stableFlight.ScheduledLandingTime.ShouldBe(unstableFlight.ScheduledLandingTime.Add(_landingRate));
        stableFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));
    }
    
    [Fact]
    public async Task UnstableFlight_WithNewEstimates_EarlierThanStable_UnstableFlightIsDelayed()
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
        await sequence.Add(firstFlight, CancellationToken.None);
        await sequence.Add(secondFlight, CancellationToken.None);
        
        // First pass
        foreach (var flight in sequence.Flights)
        {
            _scheduler.Schedule(sequence, flight);
        }
        
        // Sanity check
        firstFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime);
        firstFlight.ScheduledLandingTime.ShouldBe(firstFlight.EstimatedLandingTime);
        firstFlight.TotalDelay.ShouldBe(TimeSpan.Zero);
        
        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.EstimatedFeederFixTime.Value.Add(_landingRate));
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
        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime.Value.Add(_landingRate));
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
    public async Task NewFlight_EarlierThanSuperStable_NewFlightIsDelayed(State existingState, State newFlightState)
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();
        
        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        await sequence.Add(firstFlight, CancellationToken.None);
        
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
        
        await sequence.Add(secondFlight, CancellationToken.None);
        
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

        secondFlight.ScheduledFeederFixTime.ShouldBe(firstFlight.ScheduledFeederFixTime.Value.Add(_landingRate));
        secondFlight.ScheduledLandingTime.ShouldBe(firstFlight.ScheduledLandingTime.Add(_landingRate));
        secondFlight.TotalDelay.ShouldBe(TimeSpan.FromMinutes(4));
    }

    [Fact]
    public async Task MultipleFlights_SameRunway_SeparatedByRunwayRate()
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
        
        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        await sequence.Add(first, CancellationToken.None);
        await sequence.Add(second, CancellationToken.None);
        await sequence.Add(third, CancellationToken.None);
        
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
        second.ScheduledFeederFixTime.ShouldBe(first.ScheduledFeederFixTime.Value.Add(_landingRate));
        second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime.Add(_landingRate));
        
        third.ScheduledFeederFixTime.ShouldBe(second.ScheduledFeederFixTime.Value.Add(_landingRate));
        third.ScheduledLandingTime.ShouldBe(second.ScheduledLandingTime.Add(_landingRate));
    }

    [Fact]
    public async Task MultipleFlights_DifferentRunway_NotSeparated()
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
        await sequence.Add(first, CancellationToken.None);
        await sequence.Add(second, CancellationToken.None);
        
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
    public async Task MultipleFlights_WithNoConflicts_ShouldNotBeDelayed()
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

        var sequence = new Sequence(_airportConfigurationFixture.Instance);
        await sequence.Add(farAwayFlight, CancellationToken.None);
        await sequence.Add(closeFlight, CancellationToken.None);
        await sequence.Add(veryCloseFlight, CancellationToken.None);
        
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
    
    // TODO: Scheduled times should not be earlier than the estimate
}