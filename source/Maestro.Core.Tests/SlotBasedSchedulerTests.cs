using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests;

public class SlotBasedSchedulerTests
{
    static readonly IClock Clock = new FixedClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    readonly AirportConfigurationFixture _airportConfigurationFixture;
    readonly IRunwayAssigner _runwayAssigner;
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly IPerformanceLookup _performanceLookup;
    readonly ILogger _logger;

    public SlotBasedSchedulerTests(AirportConfigurationFixture airportConfigurationFixture)
    {
        _airportConfigurationFixture = airportConfigurationFixture;

        _airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        _airportConfigurationProvider.GetAirportConfigurations()
            .Returns([_airportConfigurationFixture.Instance]);

        _runwayAssigner = Substitute.For<IRunwayAssigner>();
        _runwayAssigner.FindBestRunways(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<RunwayAssignmentRule>>())
            .Returns(["34L", "34R"]);

        _performanceLookup = Substitute.For<IPerformanceLookup>();
        _performanceLookup.GetPerformanceDataFor("B738").Returns(new AircraftPerformanceData
        {
            Type = "B738",
            AircraftCategory = AircraftCategory.Jet,
            WakeCategory = WakeCategory.Medium
        });
        _performanceLookup.GetPerformanceDataFor("DH8D").Returns(new AircraftPerformanceData
        {
            Type = "DH8D",
            AircraftCategory = AircraftCategory.NonJet,
            WakeCategory = WakeCategory.Medium
        });

        _logger = Substitute.For<ILogger>();
    }

    [Fact]
    public void WhenNoSlotsAreAllocated_FlightIsAllocatedSlotClosestToLandingEstimate()
    {
        // Arrange
        var landingEstimate = Clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();

        var runwayMode = _airportConfigurationFixture.Instance.RunwayModes.First();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, runwayMode, Clock.UtcNow());

        // Create a real scheduler to test
        var scheduler = new SlotBasedScheduler(
            _runwayAssigner,
            _airportConfigurationProvider,
            _performanceLookup,
            _logger);

        // Act
        scheduler.AllocateSlot(sequence, flight);

        // Assert
        var allocatedSlot = sequence.Slots.FirstOrDefault(s => s.Flight == flight);
        allocatedSlot.ShouldNotBeNull();

        // Find the slot with time closest to landing estimate
        var closestSlotToEstimate = sequence.Slots
            .Where(s => s.RunwayIdentifier == "34L")
            .OrderBy(s => Math.Abs((s.Time - landingEstimate).TotalMinutes))
            .First();

        allocatedSlot.ShouldBe(closestSlotToEstimate);
        flight.ScheduledLandingTime.ShouldBe(allocatedSlot.Time);
    }

    [Fact]
    public void WhenMultipleFlightsShareLandingEstimate_TheyAreAssignedDifferentSlots()
    {
        // Arrange
        var landingEstimate = Clock.UtcNow().AddMinutes(20);
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();

        var runwayMode = _airportConfigurationFixture.Instance.RunwayModes.First();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, runwayMode, Clock.UtcNow());

        // Create a real scheduler to test
        var scheduler = new SlotBasedScheduler(
            _runwayAssigner,
            _airportConfigurationProvider,
            _performanceLookup,
            _logger);

        // Act
        scheduler.AllocateSlot(sequence, flight1);
        scheduler.AllocateSlot(sequence, flight2);

        // Assert
        var slot1 = sequence.Slots.FirstOrDefault(s => s.Flight == flight1);
        var slot2 = sequence.Slots.FirstOrDefault(s => s.Flight == flight2);

        slot1.ShouldNotBeNull();
        slot2.ShouldNotBeNull();
        slot1.ShouldNotBe(slot2);

        flight1.ScheduledLandingTime.ShouldBe(slot1.Time);
        flight2.ScheduledLandingTime.ShouldBe(slot2.Time);
    }

    [Fact]
    public void WhenMultipleRunwaysAvailable_LessPreferredRunwayWithLowerDelayIsAllocated()
    {
        // Arrange
        var landingEstimate = Clock.UtcNow().AddMinutes(20);
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();

        var runwayMode = _airportConfigurationFixture.Instance.RunwayModes.First();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, runwayMode, Clock.UtcNow());

        // Create a real scheduler to test
        var scheduler = new SlotBasedScheduler(
            _runwayAssigner,
            _airportConfigurationProvider,
            _performanceLookup,
            _logger);

        // Act
        scheduler.AllocateSlot(sequence, flight1);
        scheduler.AllocateSlot(sequence, flight2);

        // Assert
        var allocatedSlot = sequence.Slots.FirstOrDefault(s => s.Flight == flight2);
        allocatedSlot.ShouldNotBeNull();

        // Should be assigned to 34R because it results in less delay
        allocatedSlot.RunwayIdentifier.ShouldBe("34R");
        flight2.ScheduledLandingTime.ShouldBe(allocatedSlot.Time);
        flight2.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public void WhenDelayingAJet_FlightIsSetToS250()
    {
        // Arrange
        var landingEstimate = Clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();

        var runwayMode = _airportConfigurationFixture.Instance.RunwayModes.First();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, runwayMode, Clock.UtcNow());

        // Block the first available slot so the flight gets delayed
        var slotAtEstimate = sequence.Slots
            .Where(s => s.RunwayIdentifier == "34L")
            .OrderBy(s => Math.Abs((s.Time - landingEstimate).TotalMinutes))
            .First();

        var dummyFlight = new FlightBuilder("DUMMY")
            .WithLandingEstimate(slotAtEstimate.Time)
            .WithRunway("34L")
            .Build();
        slotAtEstimate.AllocateTo(dummyFlight);

        // Create a real scheduler to test
        var scheduler = new SlotBasedScheduler(
            _runwayAssigner,
            _airportConfigurationProvider,
            _performanceLookup,
            _logger);

        // Act
        scheduler.AllocateSlot(sequence, flight);

        // Assert
        var allocatedSlot = sequence.Slots.FirstOrDefault(s => s.Flight == flight);
        allocatedSlot.ShouldNotBeNull();

        // The allocated time should be later than the estimated time (delayed)
        (allocatedSlot.Time > landingEstimate).ShouldBeTrue();

        // Flight should be set to S250 flow control
        flight.FlowControls.ShouldBe(FlowControls.S250);
    }

    [Fact]
    public void WhenDelayingANonJet_FlightIsSetToProfileSpeed()
    {
        // Arrange
        var landingEstimate = Clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA1")
            .WithAircraftType("DH8D")
            .WithLandingEstimate(landingEstimate)
            .WithRunway("34L")
            .Build();

        var runwayMode = _airportConfigurationFixture.Instance.RunwayModes.First();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, runwayMode, Clock.UtcNow());

        // Block the first available slot so the flight gets delayed
        var slotAtEstimate = sequence.Slots
            .Where(s => s.RunwayIdentifier == "34L")
            .OrderBy(s => Math.Abs((s.Time - landingEstimate).TotalMinutes))
            .First();

        var dummyFlight = new FlightBuilder("DUMMY")
            .WithLandingEstimate(slotAtEstimate.Time)
            .WithRunway("34L")
            .Build();
        slotAtEstimate.AllocateTo(dummyFlight);

        // Create a real scheduler to test
        var scheduler = new SlotBasedScheduler(
            _runwayAssigner,
            _airportConfigurationProvider,
            _performanceLookup,
            _logger);

        // Act
        scheduler.AllocateSlot(sequence, flight);

        // Assert
        var allocatedSlot = sequence.Slots.FirstOrDefault(s => s.Flight == flight);
        allocatedSlot.ShouldNotBeNull();

        // The allocated time should be later than the estimated time (delayed)
        (allocatedSlot.Time > landingEstimate).ShouldBeTrue();

        // Flight should be set to ProfileSpeed flow control
        flight.FlowControls.ShouldBe(FlowControls.ProfileSpeed);
    }
}
