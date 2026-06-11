using Maestro.Contracts.Shared;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class SequenceTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _time = clockFixture.Instance.UtcNow();
    readonly TimeSpan _acceptanceRate = TimeSpan.FromSeconds(180);

    [Fact]
    public void Schedule_FlightsOnSameRunway_AreSeparatedByAcceptanceRate()
    {
        // Arrange
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(12))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(7))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        // Act
        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Assert
        var actualSeparation = flight2.LandingTime - flight1.LandingTime;
        actualSeparation.ShouldBeGreaterThanOrEqualTo(_acceptanceRate,
            "flights on the same runway should be separated by at least the acceptance rate");

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate,
            "first flight should land at its estimate");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_acceptanceRate),
            "second flight should land exactly one acceptance rate after the first");
    }

    [Fact]
    public void Schedule_FlightsOnSeparateRunways_AreSeparatedByDependencyRate()
    {
        // Arrange
        var dependencyRateSeconds = 90;
        var dependencyRate = TimeSpan.FromSeconds(dependencyRateSeconds);
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate, dependencyRateSeconds: dependencyRateSeconds);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(11))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(6))
            .WithRunway("34R")
            .WithState(State.Stable)
            .Build();

        // Act
        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Assert
        var actualSeparation = flight2.LandingTime - flight1.LandingTime;
        actualSeparation.ShouldBeGreaterThanOrEqualTo(dependencyRate,
            "flights on separate runways should be separated by at least the dependency rate");

        flight1.LandingTime.ShouldBe(flight1.LandingEstimate,
            "first flight should land at its estimate");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(dependencyRate),
            "second flight should land exactly one dependency rate after the first");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void Schedule_WhenNoRunwayIsAssigned_AndOneRunwayIsAvailable_ThatRunwayIsAssigned(State flightState)
    {
        // Arrange
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway(string.Empty)
            .WithState(flightState)
            .Build();

        // Act
        sequence.Insert(0, flight);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34L",
            $"flight in {flightState} state with no assigned runway should be assigned to the only available runway");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void Schedule_WhenNoRunwayIsAssigned_AndMultipleRunwaysAreAvailable_TheRunwayWithEarliestSTAIsAssigned(State flightState)
    {
        // Arrange
        var dependencyRateSeconds = 90;
        var dependencyRate = TimeSpan.FromSeconds(dependencyRateSeconds);
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate, dependencyRateSeconds: dependencyRateSeconds);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Insert an existing flight on 34L to create a delay
        var existingFlight = new FlightBuilder("EXISTING")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, existingFlight);

        // New flight that would conflict with existing flight if assigned to 34L
        var newFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway(string.Empty)
            .WithState(flightState)
            .Build();

        // Act
        sequence.Insert(1, newFlight);

        // Assert
        newFlight.AssignedRunwayIdentifier.ShouldBe("34R",
            $"flight in {flightState} state should be assigned to 34R as it provides the earliest landing time due to the dependency rate");
        newFlight.LandingTime.ShouldBe(existingFlight.LandingTime.Add(dependencyRate),
            "flight should land at its estimate on 34R without delay");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void Schedule_WhenNoRunwayIsAssigned_AndMultipleRunwaysHaveFeederFixRequirements_TheRunwayWithMatchingFeederFixIsAssigned(State flightState)
    {
        // Arrange
        var airportConfig = CreateDualRunwayConfiguration(
            _acceptanceRate,
            runway34LFeederFixes: ["RIVET", "WELSH"],
            runway34RFeederFixes: ["BOREE", "YAKKA"]);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flightViaRivet = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway(string.Empty)
            .WithState(flightState)
            .Build();

        var flightViaBoree = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(_time.AddMinutes(10))
            .WithRunway(string.Empty)
            .WithState(flightState)
            .Build();

        // Act
        sequence.Insert(0, flightViaRivet);
        sequence.Insert(1, flightViaBoree);

        // Assert
        flightViaRivet.AssignedRunwayIdentifier.ShouldBe("34L",
            $"flight via RIVET in {flightState} state should be assigned to 34L which accepts RIVET arrivals");
        flightViaBoree.AssignedRunwayIdentifier.ShouldBe("34R",
            $"flight via BOREE in {flightState} state should be assigned to 34R which accepts BOREE arrivals");
    }

    [Fact]
    public void Schedule_WhenFlightIsUnstable_AndMultipleRunwaysAreAvailable_TheRunwayWithEarliestSTAIsAssigned()
    {
        // Arrange
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Insert an existing flight on 34L
        var existingFlight = new FlightBuilder("EXISTING")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, existingFlight);

        // Insert unstable flight initially assigned to 34L
        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        // Act
        sequence.Insert(1, unstableFlight);

        // Assert
        unstableFlight.AssignedRunwayIdentifier.ShouldBe("34R",
            "unstable flight should be assigned to 34R as it provides the earliest landing time");
        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate,
            "unstable flight should land at its estimate as there is no dependency on flights on the other runway");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void Schedule_WhenAutomaticFlight_AndAnotherInModeRunwayLandsEarlier_IsReassigned(State stableFlightState)
    {
        // Arrange
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Insert an existing flight on 34L to create a delay
        var existingFlight = new FlightBuilder("EXISTING")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, existingFlight);

        // Insert an automatically-assigned flight on 34L; 34R (in-mode, empty) lands earlier
        var stableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        // Act
        sequence.Insert(1, stableFlight);

        // Assert
        stableFlight.AssignedRunwayIdentifier.ShouldBe("34R",
            $"automatic flight in {stableFlightState} state should be reassigned to 34R which lands earlier");
        stableFlight.LandingTime.ShouldBe(existingFlight.LandingTime,
            "the reassigned flight should land unconstrained on the empty runway");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void Schedule_WhenManualFlight_AndAnotherInModeRunwayLandsEarlier_RunwayIsNotChanged(State stableFlightState)
    {
        // Arrange
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Insert an existing flight on 34L to create a delay
        var existingFlight = new FlightBuilder("EXISTING")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, existingFlight);

        // Insert a manually-assigned flight on 34L; even though 34R lands earlier, the manual assignment is locked
        var manualFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithManualRunway("34L")
            .WithApproachType("A")
            .WithState(stableFlightState)
            .Build();

        // Act
        sequence.Insert(1, manualFlight);

        // Assert
        manualFlight.AssignedRunwayIdentifier.ShouldBe("34L",
            $"manual flight in {stableFlightState} state should remain on 34L even though 34R provides earlier landing");
        manualFlight.ApproachType.ShouldBe("A",
            $"manual flight in {stableFlightState} state should retain its approach type");
        manualFlight.LandingTime.ShouldBe(existingFlight.LandingTime.Add(_acceptanceRate),
            "manual flight's STA should be adjusted for separation but runway should not change");
    }

    [Fact]
    public void Schedule_WhenFlightIsAssignedOffModeRunway_IsSeparatedByOffModeRate()
    {
        // Arrange
        var offModeSeparation = TimeSpan.FromSeconds(300);
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate, offModeSeparationSeconds: (int)offModeSeparation.TotalSeconds);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Insert a flight on an in-mode runway (34L)
        var inModeFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, inModeFlight);

        // Insert a stable flight on an off-mode runway (16L is not in 34IVA mode)
        var offModeFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(11))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(6))
            .WithManualRunway("16L")
            .WithApproachType("A")
            .WithState(State.Stable)
            .Build();

        // Act
        sequence.Insert(1, offModeFlight);

        // Assert
        var actualSeparation = offModeFlight.LandingTime - inModeFlight.LandingTime;
        actualSeparation.ShouldBeGreaterThanOrEqualTo(offModeSeparation,
            "off-mode runway flight should be separated by the off-mode separation rate");

        offModeFlight.AssignedRunwayIdentifier.ShouldBe("16L",
            "stable flight should retain its off-mode runway assignment");
        offModeFlight.LandingTime.ShouldBe(inModeFlight.LandingTime.Add(offModeSeparation),
            "off-mode flight should land exactly one off-mode separation after the in-mode flight");
    }

    [Fact]
    public void Schedule_WhenOffModeFlightIsDisplacedPastModeChangeBoundary_OffModeSeparationIsApplied()
    {
        // Arrange
        var offModeSeparation = TimeSpan.FromSeconds(300);
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate, offModeSeparationSeconds: (int)offModeSeparation.TotalSeconds);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Pending mode change: last 34IVA landing at T+659s, first 16IVA landing at T+720s.
        // Boundary chosen so existing_34L.STA (T+480s) + acceptanceRate (180s) = T+660s exceeds
        // lastLandingInOldMode (T+659s), forcing the off-mode flight into 16IVA territory via
        // the backward search in EvaluateRunwayOption.
        var lastLandingInOldMode = _time.AddSeconds(659);
        var firstLandingInNewMode = _time.AddSeconds(720);
        var newMode = new RunwayMode(
            "16IVA",
            [
                new Runway("16L", string.Empty, _acceptanceRate, []),
                new Runway("16R", string.Empty, _acceptanceRate, [])
            ],
            dependencyRate: TimeSpan.Zero,
            offModeSeparation: offModeSeparation);
        sequence.ChangeRunwayMode(newMode, lastLandingInOldMode, firstLandingInNewMode);

        // Occupies the last available 34L slot in 34IVA territory
        var existing34L = new FlightBuilder("EXISTING_34L")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddSeconds(480))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();
        sequence.Insert(0, existing34L);

        // STA determines the 300s off-mode constraint for the off-mode flight in 16IVA territory
        var existing34R = new FlightBuilder("EXISTING_34R")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddSeconds(600))
            .WithRunway("34R")
            .WithState(State.Stable)
            .Build();
        sequence.Insert(1, existing34R);

        // Stable flight on 34L (off-mode in 16IVA), ETA just after existing34L.
        // Cannot fit in 34IVA territory (earliest T+660s > lastLanding T+659s), so the scheduler
        // searches backward past the mode change boundary into 16IVA territory.
        var offModeFlight = new FlightBuilder("OFF_MODE")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddSeconds(490))
            .WithManualRunway("34L")
            .WithState(State.Stable)
            .Build();

        // Act
        sequence.Insert(1, offModeFlight);

        // Assert
        offModeFlight.AssignedRunwayIdentifier.ShouldBe("34L",
            "stable flight should retain its off-mode runway assignment");
        offModeFlight.LandingTime.ShouldBe(existing34R.LandingTime.Add(offModeSeparation),
            "off-mode flight displaced past mode change boundary must have off-mode separation from the preceding runway flight");
    }

    [Fact]
    public void Schedule_WhenAutomaticFlightIsDisplacedPastModeChangeBoundary_IsReassignedToNewModeRunway()
    {
        // Arrange
        var offModeSeparation = TimeSpan.FromSeconds(300);
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate, offModeSeparationSeconds: (int)offModeSeparation.TotalSeconds);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Pending mode change from 34IVA to 16IVA.
        var lastLandingInOldMode = _time.AddSeconds(659);
        var firstLandingInNewMode = _time.AddSeconds(720);
        var newMode = new RunwayMode(
            "16IVA",
            [
                new Runway("16L", string.Empty, _acceptanceRate, []),
                new Runway("16R", string.Empty, _acceptanceRate, [])
            ],
            dependencyRate: TimeSpan.Zero,
            offModeSeparation: offModeSeparation);
        sequence.ChangeRunwayMode(newMode, lastLandingInOldMode, firstLandingInNewMode);

        // Occupy the last available slots on both in-mode runways in 34IVA territory so the displaced
        // flight cannot fit on either before the boundary (next slot T+660s > lastLanding T+659s).
        var existing34L = new FlightBuilder("EXISTING_34L")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddSeconds(480))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();
        sequence.Insert(0, existing34L);

        var existing34R = new FlightBuilder("EXISTING_34R")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddSeconds(485))
            .WithRunway("34R")
            .WithState(State.Stable)
            .Build();
        sequence.Insert(1, existing34R);

        // Automatically-assigned flight on 34L, ETA just after the existing flights. It cannot fit in
        // 34IVA territory on either runway (earliest T+660s/T+665s > lastLanding T+659s), so it is
        // displaced past the mode change boundary into 16IVA territory, where the 34s are off-mode.
        // Because the assignment is automatic, the scheduler must reassign it to an in-mode (16IVA)
        // runway rather than leave it off-mode.
        var displacedFlight = new FlightBuilder("DISPLACED")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddSeconds(490))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        // Act
        sequence.Insert(2, displacedFlight);

        // Assert
        displacedFlight.AssignedRunwayIdentifier.ShouldBeOneOf(["16L", "16R"],
            "an automatic flight displaced past the mode change boundary must be reassigned to an in-mode runway");
        displacedFlight.LandingTime.ShouldBeGreaterThanOrEqualTo(firstLandingInNewMode,
            "the displaced flight should land in the new mode after the mode change boundary");
    }

    [Theory]
    [InlineData(true)]  // manual assignment in the old mode
    [InlineData(false)] // automatic assignment in the old mode
    public void ChangeRunwayMode_WhenScheduled_FlightAfterBoundaryBecomesAutomaticOnNewModeRunway(bool initiallyManual)
    {
        // Arrange
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Flight on a 34IVA runway, landing after the pending boundary.
        var flightBuilder = new FlightBuilder("ABC123")
            .WithFeederFix(null)
            .WithLandingEstimate(_time.AddMinutes(30))
            .WithState(State.Stable);
        flightBuilder = initiallyManual
            ? flightBuilder.WithManualRunway("34L")
            : flightBuilder.WithRunway("34L");
        var flight = flightBuilder.Build();
        sequence.Insert(0, flight);

        var newMode = new RunwayMode(
            "16IVA",
            [
                new Runway("16L", string.Empty, _acceptanceRate, []),
                new Runway("16R", string.Empty, _acceptanceRate, [])
            ],
            dependencyRate: TimeSpan.Zero,
            offModeSeparation: TimeSpan.FromSeconds(300));

        // Act: schedule a mode change for T+20, before the flight's landing time
        sequence.ChangeRunwayMode(newMode, _time.AddMinutes(20), _time.AddMinutes(20));

        // Assert
        flight.RunwayAssignment.ShouldBeOfType<AutomaticRunwayAssignment>(
            "a scheduled mode change should clear the manual assignment so the algorithm manages the runway");
        flight.AssignedRunwayIdentifier.ShouldBeOneOf(["16L", "16R"],
            "the flight should be reassigned to a runway in the new mode");
    }

    [Fact]
    public void Schedule_WhenInModeFlightFollowsOffModeFlight_IsSeparatedByOffModeRate()
    {
        // Arrange
        var offModeSeparation = TimeSpan.FromSeconds(300);
        var airportConfig = CreateDualRunwayConfiguration(_acceptanceRate, offModeSeparationSeconds: (int)offModeSeparation.TotalSeconds);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Off-mode flight (16L is not in 34IVA mode) lands first.
        var offModeFlight = new FlightBuilder("OFF_MODE")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithManualRunway("16L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, offModeFlight);

        // In-mode flight (34L) lands shortly after, so it is scheduled against the
        // off-mode flight as its predecessor. An off-mode flight must be separated
        // from all other flights by the off-mode rate, regardless of direction.
        var inModeFlight = new FlightBuilder("IN_MODE")
            .WithLandingEstimate(_time.AddMinutes(11))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        // Act
        sequence.Insert(1, inModeFlight);

        // Assert
        inModeFlight.LandingTime.ShouldBe(offModeFlight.LandingTime.Add(offModeSeparation),
            "in-mode flight following an off-mode flight must be separated by the off-mode rate");
    }

    [Fact]
    public void ChangeLandingRates_FlightsLandingAfterChangeTime_UseNewRate()
    {
        // Arrange
        var newAcceptanceRate = TimeSpan.FromSeconds(300);
        var changeTime = _time.AddMinutes(11);
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(13))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(16))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(11))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);
        sequence.Insert(2, flight3);

        // Act
        sequence.ChangeLandingRates(new Dictionary<string, TimeSpan> { ["34L"] = newAcceptanceRate }, changeTime);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate,
            "the first flight lands before the change time and is unaffected");
        (flight2.LandingTime - flight1.LandingTime).ShouldBe(newAcceptanceRate,
            "the second flight lands after the change time and is separated by the new rate");
        (flight3.LandingTime - flight2.LandingTime).ShouldBe(newAcceptanceRate,
            "the third flight lands after the change time and is separated by the new rate");
    }

    [Fact]
    public void ChangeLandingRates_FlightsLandingBeforeChangeTime_UseOldRate()
    {
        // Arrange
        var newAcceptanceRate = TimeSpan.FromSeconds(300);
        var changeTime = _time.AddMinutes(30);
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(12))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(7))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Act
        sequence.ChangeLandingRates(new Dictionary<string, TimeSpan> { ["34L"] = newAcceptanceRate }, changeTime);

        // Assert
        (flight2.LandingTime - flight1.LandingTime).ShouldBe(_acceptanceRate,
            "both flights land before the change time and are separated by the old rate");
    }

    [Fact]
    public void ChangeLandingRates_WithRateForRunwayNotInCurrentMode_Throws()
    {
        // Arrange
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Act / Assert
        Should.Throw<MaestroException>(() =>
            sequence.ChangeLandingRates(
                new Dictionary<string, TimeSpan> { ["34R"] = TimeSpan.FromSeconds(300) },
                _time.AddMinutes(10)));
    }

    [Fact]
    public void ChangeLandingRates_StoresPendingChange()
    {
        // Arrange
        var newAcceptanceRate = TimeSpan.FromSeconds(300);
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        // Act
        sequence.ChangeLandingRates(new Dictionary<string, TimeSpan> { ["34L"] = newAcceptanceRate }, _time.AddMinutes(10));

        // Assert
        var pending = sequence.PendingConfigurationChange.ShouldBeOfType<LandingRatesChange>();
        pending.ChangeTime.ShouldBe(_time.AddMinutes(10));
        pending.NewLandingRates["34L"].ShouldBe(newAcceptanceRate);
        sequence.CurrentRunwayMode.Runways.Single(r => r.Identifier == "34L").AcceptanceRate.ShouldBe(_acceptanceRate,
            "the current runway mode is unchanged until the change time is reached");
    }

    [Fact]
    public void CancelTerminalConfigurationChange_AfterLandingRatesChange_RevertsToOldRate()
    {
        // Arrange
        var newAcceptanceRate = TimeSpan.FromSeconds(300);
        var changeTime = _time.AddMinutes(11);
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(13))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);
        sequence.ChangeLandingRates(new Dictionary<string, TimeSpan> { ["34L"] = newAcceptanceRate }, changeTime);

        // Act
        sequence.CancelConfigurationChange();

        // Assert
        sequence.PendingConfigurationChange.ShouldBeNull();
        (flight2.LandingTime - flight1.LandingTime).ShouldBe(_acceptanceRate,
            "cancelling the change reverts separation to the old rate");
    }

    [Fact]
    public void TrySwapTerminalConfiguration_WhenChangeTimeReached_PromotesNewRate()
    {
        // Arrange
        var newAcceptanceRate = TimeSpan.FromSeconds(300);
        var changeTime = _time.AddMinutes(10);
        var clock = clockFixture.Instance;
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clock)
            .Build();

        sequence.ChangeLandingRates(new Dictionary<string, TimeSpan> { ["34L"] = newAcceptanceRate }, changeTime);

        // Act
        clock.SetTime(changeTime);
        var swapped = sequence.TrySwapTerminalConfiguration();

        // Assert
        swapped.ShouldBeTrue();
        sequence.PendingConfigurationChange.ShouldBeNull();
        sequence.CurrentRunwayMode.Runways.Single(r => r.Identifier == "34L").AcceptanceRate.ShouldBe(newAcceptanceRate,
            "the new rate becomes the current runway mode after the change time is reached");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void Schedule_WhenUnstableFlightAheadHasEtaChange_StableFlightBehindDoesNotMove(State stableFlightState)
    {
        // Arrange
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        sequence.Insert(0, unstableFlight);
        sequence.Insert(1, stableFlight);

        var stableLandingTimeBefore = stableFlight.LandingTime;
        stableLandingTimeBefore.ShouldBe(_time.AddMinutes(15));

        // Act: unstable flight's ETA moves earlier, then sequence is recomputed without forcing
        unstableFlight.UpdateLandingEstimate(_time.AddMinutes(8));
        sequence.Schedule(0);

        // Assert
        stableFlight.LandingTime.ShouldBe(stableLandingTimeBefore,
            $"{stableFlightState} flight's landing time must not change when an unstable flight ahead recomputes");
        unstableFlight.LandingTime.ShouldBe(_time.AddMinutes(8),
            "unstable flight takes its new estimate");
    }

    [Fact]
    public void Schedule_WhenUnstableFlightConflictsWithStableBehind_UnstableIsMovedBehindStable()
    {
        // Arrange
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, unstableFlight);
        sequence.Insert(1, stableFlight);

        var stableLandingTimeBefore = stableFlight.LandingTime;

        // Act: unstable flight slows down into conflict with stable behind it
        unstableFlight.UpdateLandingEstimate(_time.AddMinutes(14));
        sequence.Schedule(0);

        // Assert
        stableFlight.LandingTime.ShouldBe(stableLandingTimeBefore,
            "stable flight's landing time must not move to accommodate a delayed unstable flight");
        sequence.IndexOf(stableFlight).ShouldBe(0,
            "stable flight should now lead the sequence");
        sequence.IndexOf(unstableFlight).ShouldBe(1,
            "unstable flight should be pushed behind the stable flight");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_acceptanceRate),
            "unstable flight must be separated from the stable flight by the acceptance rate");
    }

    [Fact]
    public void Schedule_WithForceRescheduleStable_RecomputesStableFlights()
    {
        // Arrange
        var airportConfig = CreateSingleRunwayConfiguration("34L", _acceptanceRate);
        var sequence = new SequenceBuilder(airportConfig)
            .WithClock(clockFixture.Instance)
            .Build();

        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(0, unstableFlight);
        sequence.Insert(1, stableFlight);

        // Act: change unstable ETA, then force a reschedule of stable flights
        unstableFlight.UpdateLandingEstimate(_time.AddMinutes(8));
        sequence.Schedule(0, forceRescheduleStable: true);

        // Assert
        unstableFlight.LandingTime.ShouldBe(_time.AddMinutes(8),
            "unstable flight takes its new estimate");
        stableFlight.LandingTime.ShouldBe(_time.AddMinutes(15),
            "stable flight retains its earlier landing estimate when unconstrained");
    }

    static AirportConfiguration CreateSingleRunwayConfiguration(string runwayIdentifier, TimeSpan acceptanceRate)
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways(runwayIdentifier)
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = runwayIdentifier,
                LandingRateSeconds = (int)acceptanceRate.TotalSeconds,
                FeederFixes = []
            })
            .Build();
    }

    static AirportConfiguration CreateDualRunwayConfiguration(
        TimeSpan acceptanceRate,
        int? dependencyRateSeconds = null,
        int? offModeSeparationSeconds = null,
        string[]? runway34LFeederFixes = null,
        string[]? runway34RFeederFixes = null)
    {
        var runwayModeConfig = new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            DependencyRateSeconds = dependencyRateSeconds ?? 0,
            OffModeSeparationSeconds = offModeSeparationSeconds ?? 0,
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = (int)acceptanceRate.TotalSeconds,
                    FeederFixes = runway34LFeederFixes ?? []
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = (int)acceptanceRate.TotalSeconds,
                    FeederFixes = runway34RFeederFixes ?? []
                }
            ]
        };

        return new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithRunwayMode(runwayModeConfig)
            .Build();
    }
}
