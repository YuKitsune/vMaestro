using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class FlightTests(ClockFixture clockFixture)
{
    readonly TimeSpan _defaultTtg = TimeSpan.FromMinutes(20);
    readonly DateTimeOffset _landingTime = clockFixture.Instance.UtcNow();
    readonly AirportConfiguration _airportConfiguration = new AirportConfigurationBuilder("YSSY").Build();

    [Fact]
    public void WhenFeederFixEstimateChanges_LandingEstimateIsUpdated()
    {
        // Arrange
        var trajectory = new TerminalTrajectory(_defaultTtg, default, default);
        var initialFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(initialFeederFixEstimate)
            .WithTrajectory(trajectory)
            .Build();

        var initialLandingEstimate = flight.LandingEstimate;
        initialLandingEstimate.ShouldBe(initialFeederFixEstimate.Add(trajectory.NormalTimeToGo));

        // Act
        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        flight.UpdateFeederFixEstimate(newFeederFixEstimate);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(newFeederFixEstimate.Add(trajectory.NormalTimeToGo));
        flight.LandingEstimate.ShouldNotBe(initialLandingEstimate);
    }

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
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after slowing down (update via feeder fix estimate, ETA = ETA_FF + TTG)
        flight.UpdateFeederFixEstimate(_landingTime.AddMinutes(2).Subtract(_defaultTtg));
        flight.SetRemainingDelayData(new DelayDistribution(
            flight.FeederFixTime - flight.FeederFixEstimate,
            (flight.LandingTime - flight.LandingEstimate) - (flight.FeederFixTime - flight.FeederFixEstimate),
            DelayStrategyCalculator.GetControlAction(flight.LandingTime - flight.LandingEstimate, flight.TerminalTrajectory, flight.EnrouteTrajectory, DelayStrategy.EnrouteFirst)));

        // Assert
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(3));

        // Act: New estimate after slowing down
        flight.UpdateFeederFixEstimate(_landingTime.AddMinutes(5).Subtract(_defaultTtg));
        flight.SetRemainingDelayData(new DelayDistribution(
            flight.FeederFixTime - flight.FeederFixEstimate,
            (flight.LandingTime - flight.LandingEstimate) - (flight.FeederFixTime - flight.FeederFixEstimate),
            DelayStrategyCalculator.GetControlAction(flight.LandingTime - flight.LandingEstimate, flight.TerminalTrajectory, flight.EnrouteTrajectory, DelayStrategy.EnrouteFirst)));

        // Assert
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.Zero);
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
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after speeding up (update via feeder fix estimate, ETA = ETA_FF + TTG)
        flight.UpdateFeederFixEstimate(_landingTime.AddMinutes(-2).Subtract(_defaultTtg));
        flight.SetRemainingDelayData(new DelayDistribution(
            flight.FeederFixTime - flight.FeederFixEstimate,
            (flight.LandingTime - flight.LandingEstimate) - (flight.FeederFixTime - flight.FeederFixEstimate),
            DelayStrategyCalculator.GetControlAction(flight.LandingTime - flight.LandingEstimate, flight.TerminalTrajectory, flight.EnrouteTrajectory, DelayStrategy.EnrouteFirst)));

        // Assert
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(7));

        // Act: New estimate after speeding up more
        flight.UpdateFeederFixEstimate(_landingTime.AddMinutes(-5).Subtract(_defaultTtg));
        flight.SetRemainingDelayData(new DelayDistribution(
            flight.FeederFixTime - flight.FeederFixEstimate,
            (flight.LandingTime - flight.LandingEstimate) - (flight.FeederFixTime - flight.FeederFixEstimate),
            DelayStrategyCalculator.GetControlAction(flight.LandingTime - flight.LandingEstimate, flight.TerminalTrajectory, flight.EnrouteTrajectory, DelayStrategy.EnrouteFirst)));

        // Assert
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(10));
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
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));

        // Act: New estimate after slowing down too much (update via feeder fix estimate, ETA = ETA_FF + TTG)
        flight.UpdateFeederFixEstimate(_landingTime.AddMinutes(8).Subtract(_defaultTtg));
        flight.SetRemainingDelayData(new DelayDistribution(
            flight.FeederFixTime - flight.FeederFixEstimate,
            (flight.LandingTime - flight.LandingEstimate) - (flight.FeederFixTime - flight.FeederFixEstimate),
            DelayStrategyCalculator.GetControlAction(flight.LandingTime - flight.LandingEstimate, flight.TerminalTrajectory, flight.EnrouteTrajectory, DelayStrategy.EnrouteFirst)));

        // Assert
        (flight.RequiredEnrouteDelay + flight.RequiredTerminalDelay).ShouldBe(TimeSpan.FromMinutes(5));
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(-3));
    }

    [Fact]
    public void WhenRemainingDelayIsNegative_NegativeDelayIsAllocatedToEnroute()
    {
        // Arrange: flight with 5 mins of enroute delay (STA_FF = ETA_FF + 5min)
        var feederFixEta = _landingTime.AddMinutes(-20);
        var feederFixSta = feederFixEta.AddMinutes(5);

        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(_landingTime)
            .WithLandingTime(_landingTime.AddMinutes(5))
            .WithFeederFixEstimate(feederFixEta)
            .WithFeederFixTime(feederFixSta)
            .WithState(State.Stable)
            .Build();

        // Sanity: 5 mins of enroute remaining, 0 TMA
        flight.RemainingEnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5));
        flight.RemainingTerminalDelay.ShouldBe(TimeSpan.Zero);

        // Act: ETA_FF moves 8 mins later, past STA_FF - flight needs to expedite at feeder fix
        flight.UpdateFeederFixEstimate(feederFixEta.AddMinutes(8));

        var remainingEnroute = flight.FeederFixTime - flight.FeederFixEstimate;
        var remainingTotal = flight.LandingTime - flight.LandingEstimate;
        flight.SetRemainingDelayData(new DelayDistribution(remainingEnroute, remainingTotal - remainingEnroute,
            DelayStrategyCalculator.GetControlAction(remainingTotal, flight.TerminalTrajectory, flight.EnrouteTrajectory, DelayStrategy.EnrouteFirst)));

        // Assert: negative remaining delay is in enroute (STA_FF - ETA_FF = -3), not TMA
        flight.RemainingEnrouteDelay.ShouldBe(TimeSpan.FromMinutes(-3));
        flight.RemainingTerminalDelay.ShouldBe(TimeSpan.Zero);
        (flight.RemainingEnrouteDelay + flight.RemainingTerminalDelay).ShouldBe(TimeSpan.FromMinutes(-3));
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
        flight.UpdateStateBasedOnTime(clockFixture.Instance, _airportConfiguration);

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
        flight.UpdateStateBasedOnTime(clockFixture.Instance, _airportConfiguration);

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
        flight.UpdateStateBasedOnTime(clockFixture.Instance, _airportConfiguration);

        // Assert
        flight.State.ShouldBe(State.SuperStable);
    }

    [Fact]
    public void WhenAFlightIsWithinRange_ItIsFrozen()
    {
        // Arrange - Create a flight within 15 minutes of landing
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(5))
            .WithLandingTime(clockFixture.Instance.UtcNow().AddMinutes(15)) // Within frozen threshold of 15 minutes
            .WithState(State.SuperStable)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance, _airportConfiguration);

        // Assert
        flight.State.ShouldBe(State.Frozen);
    }

    [Fact]
    public void WhenAFlightLands_ItIsMarkedAsLanded()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(-15))
            .WithLandingTime(clockFixture.Instance.UtcNow()) // Scheduled time slightly out
            .WithState(State.Frozen)
            .Build();

        // Act
        flight.UpdateStateBasedOnTime(clockFixture.Instance, _airportConfiguration);

        // Assert
        flight.State.ShouldBe(State.Landed);
    }

    public class HighSpeedTests(ClockFixture clockFixture)
    {
        readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();

        [Fact]
        public void WhenNoDelayAllocated_HighSpeedIsTrue()
        {
            var flight = new FlightBuilder("QFA1")
                .WithLandingEstimate(_now.AddMinutes(20))
                .Build();

            flight.SetSequenceData(
                landingTime: _now.AddMinutes(20),
                feederFixTime: _now,
                requiredControlAction: ControlAction.NoDelay,
                enrouteDelay: TimeSpan.Zero,
                terminalDelay: TimeSpan.Zero);

            flight.HighSpeed.ShouldBeTrue();
        }

        [Fact]
        public void WhenEnrouteDelayAllocated_HighSpeedIsFalse()
        {
            var flight = new FlightBuilder("QFA1")
                .WithLandingEstimate(_now.AddMinutes(20))
                .Build();

            flight.SetSequenceData(
                landingTime: _now.AddMinutes(25),
                feederFixTime: _now.AddMinutes(5),
                requiredControlAction: ControlAction.PathStretching,
                enrouteDelay: TimeSpan.FromMinutes(5),
                terminalDelay: TimeSpan.Zero);

            flight.HighSpeed.ShouldBeFalse();
        }

        [Fact]
        public void WhenOnlyTMADelayAllocated_HighSpeedIsTrue()
        {
            var flight = new FlightBuilder("QFA1")
                .WithLandingEstimate(_now.AddMinutes(20))
                .Build();

            flight.SetSequenceData(
                landingTime: _now.AddMinutes(23),
                feederFixTime: _now,
                requiredControlAction: ControlAction.PathStretching,
                enrouteDelay: TimeSpan.Zero,
                terminalDelay: TimeSpan.FromMinutes(3));

            flight.HighSpeed.ShouldBeTrue();
        }

        [Fact]
        public void WhenSubMinuteEnrouteDelayAllocated_HighSpeedIsTrue()
        {
            // Sub-minute delays can't be displayed or actioned by ATC, so HighSpeed remains true
            var flight = new FlightBuilder("QFA1")
                .WithLandingEstimate(_now.AddMinutes(20))
                .Build();

            flight.SetSequenceData(
                landingTime: _now.AddMinutes(20).AddSeconds(30),
                feederFixTime: _now.AddSeconds(30),
                requiredControlAction: ControlAction.PathStretching,
                enrouteDelay: TimeSpan.FromSeconds(30),
                terminalDelay: TimeSpan.Zero);

            flight.HighSpeed.ShouldBeTrue();
        }

        [Fact]
        public void WhenEnrouteDelayAllocated_ThenInvalidated_HighSpeedIsTrue()
        {
            var flight = new FlightBuilder("QFA1")
                .WithLandingEstimate(_now.AddMinutes(20))
                .Build();

            flight.SetSequenceData(
                landingTime: _now.AddMinutes(25),
                feederFixTime: _now.AddMinutes(5),
                requiredControlAction: ControlAction.PathStretching,
                enrouteDelay: TimeSpan.FromMinutes(5),
                terminalDelay: TimeSpan.Zero);

            flight.HighSpeed.ShouldBeFalse();

            flight.InvalidateSequenceData();

            flight.HighSpeed.ShouldBeTrue();
        }
    }
}
