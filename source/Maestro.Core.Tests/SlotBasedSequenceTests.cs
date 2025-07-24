using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class SlotBasedSequenceTests
{
    static readonly IClock Clock = new FixedClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    readonly AirportConfigurationFixture _airportConfigurationFixture;
    readonly RunwayMode _runwayMode1;
    readonly RunwayMode _runwayMode2;
    readonly ISlotBasedScheduler _slotBasedScheduler;

    public SlotBasedSequenceTests(AirportConfigurationFixture airportConfigurationFixture)
    {
        _airportConfigurationFixture = airportConfigurationFixture;

        _runwayMode1 = new RunwayMode("34PROPS", [
            new RunwayConfiguration
            {
                Identifier = "34L",
                LandingRateSeconds = 180
            },
            new RunwayConfiguration
            {
                Identifier = "34R",
                LandingRateSeconds = 180
            }
        ]);

        _runwayMode2 = new RunwayMode(
            "16IVA",
            [
                new RunwayConfiguration
                {
                    Identifier = "16L",
                    LandingRateSeconds = 200
                },
                new RunwayConfiguration
                {
                    Identifier = "16R",
                    LandingRateSeconds = 200
                }
            ]
        );
        _slotBasedScheduler = Substitute.For<ISlotBasedScheduler>();
    }

    [Fact]
    public void ReprovisionSlotsFrom_CannotProvisionSlotsTooFarInFuture()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);
        var farFutureTime = DateTime.MaxValue.AddHours(-1);

        // Act & Assert
        Should.Throw<MaestroException>(() => sequence.ReprovisionSlotsFrom(farFutureTime, _slotBasedScheduler))
            .Message.ShouldContain("too far in the future");
    }

    [Fact]
    public void ReprovisionSlotsFrom_DeletesExistingSlotsAfterSpecifiedTime()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);
        var initialSlotCount = sequence.Slots.Count;
        initialSlotCount.ShouldBeGreaterThan(0);

        var reprovisioingTime = startTime.AddMinutes(30);

        var existingSlotsAfterReprovisioning = sequence.Slots
            .Where(s => s.Time >= reprovisioingTime)
            .ToList();

        // Act
        sequence.ReprovisionSlotsFrom(reprovisioingTime, _slotBasedScheduler);

        // Assert
        sequence.Slots.Where(s => s.Time >= reprovisioingTime).ShouldAllBe(s => !existingSlotsAfterReprovisioning.Contains(s));
        existingSlotsAfterReprovisioning.ShouldAllBe(s => !sequence.Slots.Contains(s));
    }

    [Fact]
    public void ReprovisionSlotsFrom_RetainsExistingSlotsBeforeSpecifiedTime()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);
        var initialSlotCount = sequence.Slots.Count;
        initialSlotCount.ShouldBeGreaterThan(0);

        var reprovisioingTime = startTime.AddMinutes(30);

        var existingSlotsBeforeReprovisioning = sequence.Slots
            .Where(s => s.Time < reprovisioingTime)
            .ToList();

        // Act
        sequence.ReprovisionSlotsFrom(reprovisioingTime, _slotBasedScheduler);

        // Assert
        sequence.Slots.Where(s => s.Time < reprovisioingTime).ShouldAllBe(s => existingSlotsBeforeReprovisioning.Contains(s));
    }

    [Fact]
    public void ReprovisionSlotsFrom_DoesNotCreateGapsInSequence()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);
        var reprovisioingTime = startTime.AddMinutes(30);

        // Act
        sequence.ReprovisionSlotsFrom(reprovisioingTime, _slotBasedScheduler);

        // Assert - Check for gaps by inspecting consecutive slots for each runway
        var newSlots = sequence.Slots
            .Where(s => s.Time >= reprovisioingTime)
            .OrderBy(s => s.Time)
            .ToList();

        foreach (var runway in _runwayMode1.Runways)
        {
            var runwaySlots = newSlots
                .Where(s => s.RunwayIdentifier == runway.Identifier)
                .OrderBy(s => s.Time)
                .ToList();

            for (var i = 0; i < runwaySlots.Count - 1; i++)
            {
                var currentSlot = runwaySlots[i];
                var nextSlot = runwaySlots[i + 1];
                (nextSlot.Time - (currentSlot.Time + currentSlot.Duration)).TotalSeconds.ShouldBe(0);
            }
        }
    }

    [Fact]
    public void ReprovisionSlotsFrom_ReschedulesAffectedFlights()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        // Allocate flights to some slots
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(Clock.UtcNow().AddMinutes(10))
            .Build();
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(Clock.UtcNow().AddMinutes(25))
            .Build();
        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(Clock.UtcNow().AddMinutes(30))
            .Build();

        var slot1 = sequence.Slots.First(s => s.Time >= startTime.AddMinutes(10));
        var slot2 = sequence.Slots.First(s => s.Time >= startTime.AddMinutes(25));
        var slot3 = sequence.Slots.First(s => s.Time >= startTime.AddMinutes(30));

        slot1.AllocateTo(flight1);
        slot2.AllocateTo(flight2);
        slot3.AllocateTo(flight3);

        var reprovisioingTime = startTime.AddMinutes(20); // Only flight2 and flight3 should be affected

        // Act
        sequence.ReprovisionSlotsFrom(reprovisioingTime, _slotBasedScheduler);

        // Assert
        _slotBasedScheduler.Received(0).AllocateSlot(sequence, flight1); // Not affected
        _slotBasedScheduler.Received(1).AllocateSlot(sequence, flight2); // Affected
        _slotBasedScheduler.Received(1).AllocateSlot(sequence, flight3); // Affected
    }

    [Fact]
    public void ProvisionSlotsFrom_DoesNotDeleteExistingSlots()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);
        var initialSlots = sequence.Slots.ToList();
        initialSlots.Count.ShouldBeGreaterThan(0);

        var provisionTime = startTime.AddMinutes(30);

        // Act
        var existingSlots = sequence.Slots.ToList();
        sequence.ProvisionSlotsFrom(provisionTime);

        // Assert
        existingSlots.ShouldAllBe(s => sequence.Slots.Contains(s));

        var newSlots = sequence.Slots
            .Where(s => s.Time >= provisionTime)
            .ToList();

        newSlots.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ProvisionSlotsFrom_DoesNotCreateOverlappingSlots()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);
        var provisionTime = startTime.AddMinutes(30);

        // Act
        sequence.ProvisionSlotsFrom(provisionTime);

        // Assert - Check for overlapping slots for each runway
        foreach (var runway in _runwayMode1.Runways)
        {
            var runwaySlots = sequence.Slots
                .Where(s => s.RunwayIdentifier == runway.Identifier)
                .OrderBy(s => s.Time)
                .Where(s => s.Time >= provisionTime)
                .ToList();

            for (var i = 0; i < runwaySlots.Count - 1; i++)
            {
                var currentSlot = runwaySlots[i];
                var nextSlot = runwaySlots[i + 1];

                // End time of current slot should be <= start time of next slot
                (currentSlot.Time + currentSlot.Duration).ShouldBeLessThanOrEqualTo(nextSlot.Time);
            }
        }
    }

    [Fact]
    public void ProvisionSlotsFrom_AccountsForRunwayModeChangesInFuture()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        var changeTime = startTime.AddMinutes(60);
        sequence.ChangeRunwayMode(_runwayMode2, changeTime, _slotBasedScheduler);

        // Act
        sequence.ProvisionSlotsFrom(startTime);

        // Assert
        // Should have slots with original runway identifiers before change time
        var slotsBeforeChangeTime = sequence.Slots
            .Where(s => s.Time < changeTime)
            .ToList();

        var originalRunways = new[] { "34L", "34R" };
        var originalRate = TimeSpan.FromSeconds(180);
        slotsBeforeChangeTime.ShouldAllBe(s => originalRunways.Contains(s.RunwayIdentifier) && s.Duration == originalRate);

        var slotsAfterChangeTime = sequence.Slots
            .Where(s => s.Time >= changeTime)
            .ToList();

        var newRunways = new[] { "16L", "16R" };
        var newRate = TimeSpan.FromSeconds(200);
        slotsAfterChangeTime.ShouldAllBe(s => newRunways.Contains(s.RunwayIdentifier) && s.Duration == newRate);
    }

    [Fact]
    public void ChangeRunwayMode_Immediate_ChangesCurrentRunwayModeImmediately()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        // Act
        sequence.ChangeRunwayMode(_runwayMode2, _slotBasedScheduler, Clock);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_runwayMode2);
        sequence.NextRunwayMode.ShouldBeNull();
        sequence.RunwayModeChangeTime.ShouldBe(default);

        // Should have slots with new runway identifiers
        var newRunways = new[] { "16L", "16R" };
        var newRate = TimeSpan.FromSeconds(200);
        sequence.Slots.ShouldAllBe(s => newRunways.Contains(s.RunwayIdentifier) && s.Duration == newRate);
    }

    [Fact]
    public void ChangeRunwayMode_Future_SchedulesRunwayModeChangeForFutureTime()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        var changeTime = startTime.AddHours(1);

        // Act
        sequence.ChangeRunwayMode(_runwayMode2, changeTime, _slotBasedScheduler);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_runwayMode1); // Current mode should not change
        sequence.NextRunwayMode.ShouldBe(_runwayMode2); // Next mode should be set
        sequence.RunwayModeChangeTime.ShouldBe(changeTime); // Change time should be set
    }

    [Fact]
    public void ChangeRunwayMode_Future_ReprovisionsSlotBasedOnNewRunwayMode()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        var changeTime = startTime.AddHours(1);

        // Act
        sequence.ChangeRunwayMode(_runwayMode2, changeTime, _slotBasedScheduler);

        // Assert
        var slotsBeforeChangeTime = sequence.Slots
            .Where(s => s.Time < changeTime)
            .ToList();

        var originalRunways = new[] { "34L", "34R" };
        var originalRate = TimeSpan.FromSeconds(180);
        slotsBeforeChangeTime.ShouldAllBe(s => originalRunways.Contains(s.RunwayIdentifier) && s.Duration == originalRate);

        var slotsAfterChangeTime = sequence.Slots
            .Where(s => s.Time >= changeTime)
            .ToList();

        var newRunways = new[] { "16L", "16R" };
        var newRate = TimeSpan.FromSeconds(200);
        slotsAfterChangeTime.ShouldAllBe(s => newRunways.Contains(s.RunwayIdentifier) && s.Duration == newRate);
    }

    [Fact]
    public void ChangeRunwayMode_Future_ReschedulesAffectedFlights()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        // Allocate flights to some slots
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(Clock.UtcNow().AddMinutes(10))
            .Build();
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(Clock.UtcNow().AddMinutes(25))
            .Build();
        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(Clock.UtcNow().AddMinutes(30))
            .Build();

        var slot1 = sequence.Slots.First(s => s.Time >= startTime.AddMinutes(10));
        var slot2 = sequence.Slots.First(s => s.Time >= startTime.AddMinutes(25));
        var slot3 = sequence.Slots.First(s => s.Time >= startTime.AddMinutes(30));

        slot1.AllocateTo(flight1);
        slot2.AllocateTo(flight2);
        slot3.AllocateTo(flight3);

        var changeTime = startTime.AddMinutes(30); // Only flight3 should be affected

        // Act
        sequence.ChangeRunwayMode(_runwayMode2, changeTime, _slotBasedScheduler);

        // Assert
        _slotBasedScheduler.Received(0).AllocateSlot(sequence, flight1); // Not affected
        _slotBasedScheduler.Received(0).AllocateSlot(sequence, flight2); // Not affected
        _slotBasedScheduler.Received(1).AllocateSlot(sequence, flight3); // Affected
    }

    [Fact]
    public void RunwayModeAt_ReturnsCurrentRunwayModeBeforeChangeTime()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        var changeTime = startTime.AddHours(1);
        sequence.ChangeRunwayMode(_runwayMode2, changeTime, _slotBasedScheduler);

        // Act & Assert
        sequence.RunwayModeAt(changeTime.AddMinutes(-1)).ShouldBe(_runwayMode1);
        sequence.RunwayModeAt(startTime).ShouldBe(_runwayMode1);
    }

    [Fact]
    public void RunwayModeAt_ReturnsNextRunwayModeAfterChangeTime()
    {
        // Arrange
        var startTime = Clock.UtcNow();
        var sequence = new SlotBasedSequence(_airportConfigurationFixture.Instance, _runwayMode1, startTime);

        var changeTime = startTime.AddHours(1);
        sequence.ChangeRunwayMode(_runwayMode2, changeTime, _slotBasedScheduler);

        // Act & Assert
        sequence.RunwayModeAt(changeTime).ShouldBe(_runwayMode2);
        sequence.RunwayModeAt(changeTime.AddMinutes(1)).ShouldBe(_runwayMode2);
        sequence.RunwayModeAt(changeTime.AddHours(1)).ShouldBe(_runwayMode2);
    }
}
