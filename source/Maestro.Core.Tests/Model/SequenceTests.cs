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
    public void Schedule_WhenFlightIsStable_AndNonPreferredRunwayIsAssigned_RunwayIsNotChanged(State stableFlightState)
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

        // Insert stable flight assigned to 34L, even though 34R would provide earlier landing
        var stableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithApproachType("A")
            .WithState(stableFlightState)
            .Build();

        // Act
        sequence.Insert(1, stableFlight);

        // Assert
        stableFlight.AssignedRunwayIdentifier.ShouldBe("34L",
            $"flight in {stableFlightState} state should remain on 34L even though 34R provides earlier landing");
        stableFlight.ApproachType.ShouldBe("A",
            $"flight in {stableFlightState} state should retain its approach type");
        stableFlight.LandingTime.ShouldBe(existingFlight.LandingTime.Add(_acceptanceRate),
            "stable flight's STA should be adjusted for separation but runway should not change");
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
            .WithRunway("16L")
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
