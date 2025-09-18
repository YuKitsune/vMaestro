using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;
using RunwayDependency = Maestro.Core.Configuration.RunwayDependency;

namespace Maestro.Core.Tests.Model;

// TODO: Test cases
// Slots on one runway shouldn't affect flights on another runway
// When dependent runways are in use, flights are separated from flights on the other runway by the dependency rate
// Account for MaxDelay (no delay) flights

// ChangeRunwayMode_CancelsFutureModeChange
// Reposition_WithFlightsOnEitherSideOfRunwayModeChange_WithLessSpacingThanLandingRate_DelaysFlightsAppropriately (F1 00:10, Change at 00:12, F2 should be at 00:13, not 00:12)

public class SequenceTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly DateTimeOffset _time = clockFixture.Instance.UtcNow();
    readonly TimeSpan _landingRate = TimeSpan.FromSeconds(180);

    [Fact]
    public void ChangeRunwayMode_Immediately_ReschedulesAllFlights()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Act: Change runway mode immediately with longer landing rate
        var newLandingRate = TimeSpan.FromSeconds(240);
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34L",
            Runways = [new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = (int)newLandingRate.TotalSeconds }]
        });

        sequence.ChangeRunwayMode(newRunwayMode);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight shouldn't change");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(newLandingRate), "additional delay should be applied to the second flight");
    }

    // BUG: This test passes, but it doesn't appear to work in production
    [Fact]
    public void ChangeRunwayMode_ReschedulesFlightsAfterModeChange()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(16))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(19))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        // Act: Change runway mode after the first flight
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways = [new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = (int)_landingRate.TotalSeconds, Dependencies = [new RunwayDependency{RunwayIdentifier = "34L"}]}]
        });

        sequence.ChangeRunwayMode(newRunwayMode, _time.AddMinutes(10), _time.AddMinutes(20));

        // Assert
        flight1.LandingTime.ShouldBe(_time.AddMinutes(20), "first flight should remain unchanged");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "second flight should be delayed until the start of the new mode");
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(_landingRate), "third flight should be delayed behind the second flight");

        flight1.AssignedRunwayIdentifier.ShouldBe("34R", "first flight should be assigned to the new runway");
        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "second flight should be assigned to the new runway");
        flight3.AssignedRunwayIdentifier.ShouldBe("34R", "third flight should be assigned to the new runway");
    }

    [Fact]
    public void AddDummyFlight_AddsAFlightAtTheSpecifiedTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        var originalLandingTime = flight1.LandingTime;

        // Act: Add a dummy flight at the same time as the existing flight
        sequence.AddDummyFlight(flight1.LandingTime, ["34L"]);

        // Assert
        var sequenceMessage = sequence.ToMessage();
        var dummyFlight = sequenceMessage.Flights.FirstOrDefault(f => f.IsDummy);
        dummyFlight.ShouldNotBeNull("dummy flight should be added to sequence");
        dummyFlight.LandingTime.ShouldBe(_time.AddMinutes(5), "dummy flight should land at specified time");
        dummyFlight.State.ShouldBe(State.Frozen, "dummy flight should be frozen");

        flight1.LandingTime.ShouldBe(dummyFlight.LandingTime.Add(_landingRate), "existing flight should be delayed behind dummy flight");
        flight1.LandingTime.ShouldBeGreaterThan(originalLandingTime, "existing flight should be delayed");
    }

    [Fact]
    public void AddDummyFlight_BeforeAnotherFlight_DelaysExistingFlights()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        var originalLandingTime = flight1.LandingTime;

        // Act: Add a dummy flight before the first flight
        sequence.AddDummyFlight(RelativePosition.Before, "ABC123");

        // Assert
        var sequenceMessage = sequence.ToMessage();
        var dummyFlight = sequenceMessage.Flights.FirstOrDefault(f => f.IsDummy);
        dummyFlight.ShouldNotBeNull("dummy flight should be added to sequence");
        dummyFlight.LandingTime.ShouldBe(originalLandingTime, "dummy flight should take the first flight's original time");
        dummyFlight.State.ShouldBe(State.Frozen, "dummy flight should be frozen");

        flight1.LandingTime.ShouldBe(dummyFlight.LandingTime.Add(_landingRate), "first flight should be delayed behind dummy flight");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "second flight should be delayed behind first flight");
    }

    [Fact]
    public void AddDummyFlight_AfterAnotherFlight_DelaysTrailingFlights()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        var originalFlight1LandingTime = flight1.LandingTime;

        // Act: Add a dummy flight after the first flight
        sequence.AddDummyFlight(RelativePosition.After, "ABC123");

        // Assert
        var sequenceMessage = sequence.ToMessage();
        var dummyFlight = sequenceMessage.Flights.FirstOrDefault(f => f.IsDummy);
        dummyFlight.ShouldNotBeNull("dummy flight should be added to sequence");
        dummyFlight.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "dummy flight should be positioned just behind the first flight");
        dummyFlight.State.ShouldBe(State.Frozen, "dummy flight should be frozen");

        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "first flight should remain unchanged");
        flight2.LandingTime.ShouldBe(dummyFlight.LandingTime.Add(_landingRate), "second flight should be delayed behind dummy flight");
    }

    [Fact]
    public void AddPendingFlight_AddsFlightToPendingList()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var pendingFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        // Act: Add a flight to the pending list
        sequence.AddPendingFlight(pendingFlight);

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldContain(f => f.Callsign == "ABC123", "flight should be in pending list");
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "ABC123", "flight should not be in main sequence");
        sequenceMessage.PendingFlights.Length.ShouldBe(1, "should have exactly one pending flight");
        sequenceMessage.Flights.Length.ShouldBe(0, "main sequence should be empty");
    }

    [Fact]
    public void Desequence_RemovesFlightFromSequence()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Act: Desequence the first flight
        sequence.Desequence("ABC123");

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.DeSequencedFlights.ShouldContain(f => f.Callsign == "ABC123", "first flight should be in desequenced list");
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "ABC123", "first flight should not be in main sequence");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "DEF456", "second flight should remain in sequence");

        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should return to its original estimate");
    }

    [Fact]
    public void Resume_ResequencesADeSequencedFlight()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(7))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Desequence the second flight
        sequence.Desequence("DEF456");

        // Act: Resume the desequenced flight
        sequence.Resume("DEF456");

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.DeSequencedFlights.ShouldNotContain(f => f.Callsign == "DEF456", "resumed flight should not be in desequenced list");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "DEF456", "resumed flight should be back in main sequence");

        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "first flight should remain unchanged");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "resumed flight should be behind the first flight");
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(_landingRate), "third flight should be delayed behind the resumed flight");
    }

    [Fact]
    public void Remove_RemovesFlightFromSequence()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Act: Remove the first flight
        sequence.Remove("ABC123");

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "ABC123", "removed flight should not be in main sequence");
        sequenceMessage.DeSequencedFlights.ShouldNotContain(f => f.Callsign == "ABC123", "removed flight should not be in desequenced list");
        sequenceMessage.PendingFlights.ShouldNotContain(f => f.Callsign == "ABC123", "removed flight should not be in pending list"); // TODO: It might be intended that removed flights enter the pending list. Need to clarify.
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "DEF456", "second flight should remain in sequence");

        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should return to its original estimate");
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Stable)]
    [InlineData(State.Unstable)]
    public void CreateSlot_PreventsLanding(State secondFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .WithState(secondFlightState)
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        var originalFlight1LandingTime = flight1.LandingTime;

        // Act: Create a slot that encapsulates both flights
        var slotStart = _time.AddMinutes(4);
        var slotEnd = _time.AddMinutes(8);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"]);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "frozen flight should remain in place");
        flight2.LandingTime.ShouldBe(slotEnd, "second flight should be delayed until the end of the slot");
        flight2.LandingTime.ShouldBeGreaterThan(flight2.LandingEstimate, "second flight should be delayed from its original estimate");
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Stable)]
    [InlineData(State.Unstable)]
    public void ModifySlot_PreventsLanding(State secondFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .WithState(secondFlightState)
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        var originalFlight1LandingTime = flight1.LandingTime;

        // Create initial slot
        var initialSlotStart = _time.AddMinutes(0);
        var initialSlotEnd = _time.AddMinutes(10);
        sequence.CreateSlot(initialSlotStart, initialSlotEnd, ["34L"]);

        var sequenceMessage = sequence.ToMessage();
        var slotId = sequenceMessage.Slots.First().Id;

        // Act: Modify the slot to become shorter
        var newSlotStart = _time.AddMinutes(2);
        var newSlotEnd = _time.AddMinutes(8);
        sequence.ModifySlot(slotId, newSlotStart, newSlotEnd);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "frozen flight should remain in place");
        flight2.LandingTime.ShouldBe(newSlotEnd, "second flight should be delayed until the end of the modified slot");
        flight2.LandingTime.ShouldBeGreaterThan(flight2.LandingEstimate, "second flight should be delayed from its original estimate");
        flight2.LandingTime.ShouldBeLessThan(initialSlotEnd, "second flight should benefit from the shorter slot");
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Stable)]
    [InlineData(State.Unstable)]
    public void DeleteSlot_AllowsLanding(State secondFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .WithState(secondFlightState)
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        // Create slot encapsulating both flights
        var slotStart = _time.AddMinutes(4);
        var slotEnd = _time.AddMinutes(10);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"]);

        var sequenceMessage = sequence.ToMessage();
        var slotId = sequenceMessage.Slots.First().Id;

        // Act: Delete the slot
        sequence.DeleteSlot(slotId);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "frozen flight should remain in place");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "second flight should return to its original landing time");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should return to its estimate");
    }

    [Fact]
    public void NumberInSequence_ReflectsCurrentSequence()
    {
        // TODO: Include landed flights to ensure they're not counted

        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(7))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        // Act & Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be number 1 in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(2, "second flight should be number 2 in sequence");
        sequence.NumberInSequence(flight3).ShouldBe(3, "third flight should be number 3 in sequence");
    }

    [Fact]
    public void NumberForRunway_ReflectsCurrentSequence()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build(); // Uses default runway mode with 34L and 34R

        var flight1_34L = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2_34R = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34R")
            .Build();

        var flight3_34L = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        var flight4_34R = new FlightBuilder("JKL012")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34R")
            .Build();

        sequence.Insert(flight1_34L, flight1_34L.LandingEstimate);
        sequence.Insert(flight2_34R, flight2_34R.LandingEstimate);
        sequence.Insert(flight3_34L, flight3_34L.LandingEstimate);
        sequence.Insert(flight4_34R, flight4_34R.LandingEstimate);

        // Act & Assert
        sequence.NumberForRunway(flight1_34L).ShouldBe(1, "first flight on 34L should be number 1 for runway 34L");
        sequence.NumberForRunway(flight3_34L).ShouldBe(2, "second flight on 34L should be number 2 for runway 34L");

        sequence.NumberForRunway(flight2_34R).ShouldBe(1, "first flight on 34R should be number 1 for runway 34R");
        sequence.NumberForRunway(flight4_34R).ShouldBe(2, "second flight on 34R should be number 2 for runway 34R");
    }

    [Fact]
    public void Insert_WithNoConflict_PlacesFlightBasedOnSpecifiedTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(11)) // Well spaced apart
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(8)) // Between the two existing flights
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with estimate between the two existing flights
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should remain at its estimate");
        newFlight.LandingTime.ShouldBe(newFlight.LandingEstimate, "new flight should land at its estimate");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should remain at its estimate");

        // Verify ordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be first in sequence");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should be third in sequence");
    }

    [Fact]
    public void Insert_WithConflict_PlacesFlightBasedOnSpecifiedTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(6)) // Between the two flights, causing conflict
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with estimate that conflicts
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should remain at its estimate");
        newFlight.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "new flight should be delayed behind the first flight");
        flight2.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "second flight should be delayed behind the new flight");

        // Verify ordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be first in sequence");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should be third in sequence");
    }

    [Fact]
    public void Insert_WithEstimateAheadOfStable_DisplacedStableFlights()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var stableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(stableFlight, stableFlight.LandingEstimate);
        sequence.Insert(unstableFlight, unstableFlight.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(5)) // ETA ahead of the Stable flight
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA ahead of the Stable flight
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        newFlight.LandingTime.ShouldBe(newFlight.LandingEstimate, "new flight should land at its estimate");
        stableFlight.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "stable flight should be displayed behind the new flight");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be further delayed behind the stable flight");

        // Verify ordering
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should be first in the sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should be displaced by the new flight");
        sequence.NumberInSequence(unstableFlight).ShouldBe(3, "unstable flight should be delayed further back");
    }

    [Fact]
    public void Insert_WithEstimateAheadOfSuperStable_PlacesNewFlightBehindSuperStable()
    {
        // TODO: Use a theory to check the Frozen and Landed states too

        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var superStableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(12))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(superStableFlight, superStableFlight.LandingEstimate);
        sequence.Insert(stableFlight, stableFlight.LandingEstimate);
        sequence.Insert(unstableFlight, unstableFlight.LandingEstimate);

        var newFlight = new FlightBuilder("JKL012")
            .WithLandingEstimate(_time.AddMinutes(4)) // ETA well ahead of the SuperStable flight
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA ahead of SuperStable flight
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        superStableFlight.LandingTime.ShouldBe(superStableFlight.LandingEstimate, "SuperStable flight should remain at its estimate");
        newFlight.LandingTime.ShouldBe(superStableFlight.LandingTime.Add(_landingRate), "new flight should be placed behind the SuperStable flight");
        stableFlight.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "stable flight should be delayed behind the new flight");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind the stable flight");

        // Verify ordering
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "SuperStable flight should be first in sequence");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be placed behind the SuperStable flight");
        sequence.NumberInSequence(stableFlight).ShouldBe(3, "stable flight should be third in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(4, "unstable flight should be fourth in sequence");
    }

    [Fact]
    public void Insert_WithSlot_PlacesFlightAtEndOfSlot()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Create a slot first
        var slotStart = _time.AddMinutes(4);
        var slotEnd = _time.AddMinutes(7);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"]);

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(5)) // ETA inside the slot
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA inside the slot
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        newFlight.LandingTime.ShouldBe(slotEnd, "new flight should be placed at the end of the slot");
        flight1.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "first flight should be delayed behind the new flight");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "second flight should be delayed behind the first flight");

        // Verify ordering
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should be third in sequence");
    }

    [Fact]
    public void Insert_WithSlot_AndSuperStable_PlacesFlightAtEndOfSlot()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Create a slot first
        var slotStart = _time.AddMinutes(4);
        var slotEnd = _time.AddMinutes(7);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"]);

        var superStableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(superStableFlight, superStableFlight.LandingEstimate);
        sequence.Insert(stableFlight, stableFlight.LandingEstimate);
        sequence.Insert(unstableFlight, unstableFlight.LandingEstimate);

        var newFlight = new FlightBuilder("JKL012")
            .WithLandingEstimate(_time.AddMinutes(5)) // ETA inside the slot
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA inside the slot
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        superStableFlight.LandingTime.ShouldBe(superStableFlight.LandingEstimate, "SuperStable flight should remain at its estimate");
        newFlight.LandingTime.ShouldBe(superStableFlight.LandingTime.Add(_landingRate), "new flight should be placed behind the SuperStable flight");
        stableFlight.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "stable flight should be delayed behind the new flight");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind the stable flight");

        // Verify ordering
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "SuperStable flight should be first in sequence");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be placed behind the SuperStable flight");
        sequence.NumberInSequence(stableFlight).ShouldBe(3, "stable flight should be third in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(4, "unstable flight should be fourth in sequence");
    }

    [Fact]
    public void Insert_WithSlot_AndFrozen_PlacesFlightAtEndOfSlot()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var frozenFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        sequence.Insert(frozenFlight, frozenFlight.LandingEstimate);

        // Create a slot that encapsulates the frozen flight
        var slotStart = _time.AddMinutes(0);
        var slotEnd = _time.AddMinutes(10);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"]);

        var newFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(5)) // ETA inside the slot, near the frozen flight
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA inside the slot
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        frozenFlight.LandingTime.ShouldBe(frozenFlight.LandingEstimate, "frozen flight should remain at its estimate");
        newFlight.LandingTime.ShouldBe(slotEnd, "new flight should be placed at the end of the slot");

        // Verify ordering
        sequence.NumberInSequence(frozenFlight).ShouldBe(1, "frozen flight should be first in sequence");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be second in sequence");
    }

    [Fact]
    public void Insert_WithDelayIntoSlot_DelaysFlightUntilAfterSlot()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Create a slot scheduled in the near future
        var slotStart = _time.AddMinutes(5);
        var slotEnd = _time.AddMinutes(8);
        sequence.CreateSlot(slotStart, slotEnd, ["34L"]);

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(3))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(4))
            .WithRunway("34L")
            .Build();

        // Act: Insert flights such that the second would be delayed into the slot
        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should land at its estimate");

        // Second flight would normally be delayed to flight1.LandingTime + _landingRate = 6 minutes,
        // but this falls within the slot (5-8 minutes), so it should be delayed until after the slot
        flight2.LandingTime.ShouldBe(slotEnd, "second flight should be delayed until after the slot");
        flight2.LandingTime.ShouldBeGreaterThan(flight1.LandingTime.Add(_landingRate), "second flight should be delayed beyond normal separation due to slot");

        // Verify ordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be first in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(2, "second flight should be second in sequence");
    }

    [Fact]
    public void Insert_WithRunwayChange_PlacesFlightAtBeginningOfNewMode()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Schedule a runway mode change
        var lastLandingTimeForOldMode = _time.AddMinutes(5);
        var firstLandingTimeForNewMode = _time.AddMinutes(8);
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways = [new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = (int)_landingRate.TotalSeconds, Dependencies = [new RunwayDependency { RunwayIdentifier = "34L"}]}]
        });

        sequence.ChangeRunwayMode(newRunwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode);

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(6)) // ETA inside the change period
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA inside the runway change period
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        newFlight.LandingTime.ShouldBe(firstLandingTimeForNewMode, "new flight should be placed at the beginning of the new mode");
        flight1.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "first flight should be delayed behind the new flight");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "second flight should be delayed behind the first flight");

        // Verify ordering
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should be third in sequence");
    }

    [Fact]
    public void Insert_WithDelayIntoRunwayChange_DelaysFlightUntilAfterChange()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Schedule a runway mode change in the near future
        var lastLandingTimeForOldMode = _time.AddMinutes(5);
        var firstLandingTimeForNewMode = _time.AddMinutes(8);
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways = [new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = (int)_landingRate.TotalSeconds }]
        });

        sequence.ChangeRunwayMode(newRunwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode);

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(3))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(4))
            .WithRunway("34L")
            .Build();

        // Act: Insert flights such that the second would be delayed into the runway change period
        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should land at its estimate");

        // Second flight would normally be delayed to flight1.LandingTime + _landingRate = 6 minutes,
        // but this falls within the runway change period (5-8 minutes), so it should be delayed until after the change
        flight2.LandingTime.ShouldBe(firstLandingTimeForNewMode, "second flight should be delayed until after the runway change");
        flight2.LandingTime.ShouldBeGreaterThan(flight1.LandingTime.Add(_landingRate), "second flight should be delayed beyond normal separation due to runway change");

        // Verify ordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be first in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(2, "second flight should be second in sequence");
    }

    [Fact]
    public void Insert_WithRunwayChange_AndSuperStable_PlacesFlightAtEndOfSlot()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Schedule a runway mode change
        var lastLandingTimeForOldMode = _time.AddMinutes(5);
        var firstLandingTimeForNewMode = _time.AddMinutes(8);
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways = [new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = (int)_landingRate.TotalSeconds, Dependencies = [new RunwayDependency {RunwayIdentifier = "34L"}]}]
        });

        sequence.ChangeRunwayMode(newRunwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode);

        var superStableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(12))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(superStableFlight, superStableFlight.LandingEstimate);
        sequence.Insert(stableFlight, stableFlight.LandingEstimate);
        sequence.Insert(unstableFlight, unstableFlight.LandingEstimate);

        var newFlight = new FlightBuilder("JKL012")
            .WithLandingEstimate(_time.AddMinutes(6)) // ETA inside the runway change period
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight with ETA inside the runway change period
        sequence.Insert(newFlight, newFlight.LandingEstimate);

        // Assert
        superStableFlight.LandingTime.ShouldBe(superStableFlight.LandingEstimate, "SuperStable flight should remain at its estimate");
        newFlight.LandingTime.ShouldBe(superStableFlight.LandingTime.Add(_landingRate), "new flight should be placed behind the SuperStable flight");
        stableFlight.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "stable flight should be delayed behind the new flight");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind the stable flight");

        // Verify ordering
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "SuperStable flight should be first in sequence");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be placed behind the SuperStable flight");
        sequence.NumberInSequence(stableFlight).ShouldBe(3, "stable flight should be third in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(4, "unstable flight should be fourth in sequence");
    }

    [Fact]
    public void Insert_BeforeAnotherFlight_WithNoConflicts_PlacesFlightInFront()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15)) // Well spaced apart
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(5)) // Well before the first flight
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight before the first flight using relative positioning
        sequence.Insert(newFlight, RelativePosition.Before, "ABC123");

        // Assert
        newFlight.LandingTime.ShouldBe(newFlight.LandingEstimate, "new flight should not be delayed");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should not be delayed");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should not be delayed");

        // Verify ordering
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should become first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should become second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should become third in sequence");
    }

    [Fact]
    public void Insert_BeforeAnotherFlight_WithConflicts_PlacesFlightInFront()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var newFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        // Act: Insert new flight before the first flight using relative positioning
        sequence.Insert(newFlight, RelativePosition.Before, "ABC123");

        // Assert
        newFlight.LandingTime.ShouldBe(newFlight.LandingEstimate, "new flight should land at its estimate");
        flight1.LandingTime.ShouldBe(newFlight.LandingTime.Add(_landingRate), "first flight should be delayed behind new flight");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "second flight should be delayed behind first flight");

        // Verify ordering
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should become first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should become second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should become third in sequence");
    }

    // TODO: Insert after? (Claude, ignore this comment, we'll come back to it)

    [Fact]
    public void Recompute_WithNoConflict_PlacesFlightBasedOnEstimate()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Change the estimate of the first flight to be well after the second flight
        flight1.UpdateLandingEstimate(_time.AddMinutes(15));

        // Act: Recompute the sequence
        sequence.Recompute(flight1);

        // Assert
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should return to its estimate");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should land at its new estimate");

        // Verify ordering has changed
        sequence.NumberInSequence(flight2).ShouldBe(1, "second flight should now be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should now be second in sequence");
    }

    [Fact]
    public void Recompute_WithConflict_PlacesFlightBasedOnEstimate()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Change the estimate of the first flight to be after the second flight (but close enough to cause conflict)
        flight1.UpdateLandingEstimate(_time.AddMinutes(7));

        // Act: Recompute the sequence
        sequence.Recompute(flight1);

        // Assert
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should return to its estimate");
        flight1.LandingTime.ShouldBe(flight2.LandingTime.Add(_landingRate), "first flight should be delayed behind the second flight");

        // Verify ordering has changed
        sequence.NumberInSequence(flight2).ShouldBe(1, "second flight should now be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should now be second in sequence");
    }

    [Fact]
    public void MakePending_MovesFlightToPendingList()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Act: Make the first flight pending
        sequence.MakePending(flight1);

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldContain(f => f.Callsign == "ABC123", "first flight should be in pending list");
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "ABC123", "first flight should not be in main sequence");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "DEF456", "second flight should remain in sequence");

        // Verify sequence ordering
        sequence.NumberInSequence(flight2).ShouldBe(1, "second flight should now be first in sequence");
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public void Depart_WithConflict_DelaysBehindFixedFlights(State existingState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var existingFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(existingState)
            .Build();

        sequence.Insert(existingFlight, existingFlight.LandingEstimate);

        var pendingFlight = new FlightBuilder("DEF456")
            .WithEstimatedFlightTime(TimeSpan.FromMinutes(7)) // EET of 7 minutes
            .WithRunway("34L")
            .Build();

        sequence.AddPendingFlight(pendingFlight);

        var takeOffTime = _time.AddMinutes(2); // Takeoff at 2 minutes
        // This would result in arrival at 2 + 7 = 9 minutes, which conflicts with existing flight at 10 minutes

        // Act: Depart the pending flight
        sequence.Depart(pendingFlight, takeOffTime);

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldNotContain(f => f.Callsign == "DEF456", "departed flight should not be in pending list");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "DEF456", "departed flight should be in main sequence");

        existingFlight.LandingTime.ShouldBe(existingFlight.LandingEstimate, "existing flight should remain at its estimate");
        pendingFlight.LandingTime.ShouldBe(existingFlight.LandingTime.Add(_landingRate), "departed flight should be delayed behind existing flight");

        // Verify ordering
        sequence.NumberInSequence(existingFlight).ShouldBe(1, "existing flight should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "departed flight should be second in sequence");
    }

    // TODO: Need to verify if this behaviour is correct
    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.Unstable)]
    public void Depart_WithStableConflict_DelaysExistingFlights(State existingState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var existingFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(existingState)
            .Build();

        sequence.Insert(existingFlight, existingFlight.LandingEstimate);

        var pendingFlight = new FlightBuilder("DEF456")
            .WithEstimatedFlightTime(TimeSpan.FromMinutes(7)) // EET of 7 minutes
            .WithRunway("34L")
            .Build();

        sequence.AddPendingFlight(pendingFlight);

        var takeOffTime = _time.AddMinutes(2); // Takeoff at 2 minutes
        // This would result in arrival at 2 + 7 = 9 minutes, which conflicts with existing flight at 10 minutes

        // Act: Depart the pending flight
        sequence.Depart(pendingFlight, takeOffTime);

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldNotContain(f => f.Callsign == "DEF456", "departed flight should not be in pending list");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "DEF456", "departed flight should be in main sequence");

        pendingFlight.LandingTime.ShouldBe(pendingFlight.LandingEstimate, "departed flight should have no delay");
        existingFlight.LandingTime.ShouldBe(pendingFlight.LandingTime.Add(_landingRate), "existing flight should be delayed behind departed flight");

        // Assert state once combined with handler
        // pendingFlight.State.ShouldBe(State.Stable, "departed flight should be Stable once activated");

        // Verify ordering
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "departed flight should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "existing flight should be second in sequence");
    }

    [Fact]
    public void MoveFlight_WithNoConflict_RepositionsFlight_BasedOnTargetTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(4))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        var newLandingTime = _time.AddMinutes(7); // Between flight1 and flight2

        // Act: Move the third flight to before the second flight
        sequence.MoveFlight("GHI789", newLandingTime, ["34L"]);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should remain at its estimate");
        flight3.LandingTime.ShouldBe(newLandingTime, "moved flight should land at target time");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "second flight should remain at its estimate");

        // Verify ordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be first in sequence");
        sequence.NumberInSequence(flight3).ShouldBe(2, "moved flight should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should be third in sequence");
    }

    [Fact]
    public void MoveFlight_WithConflict_RepositionsFlight_BasedOnTargetTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(11))
            .WithRunway("34L")
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        var newLandingTime = _time.AddMinutes(7); // Close to flight1, causing conflict

        // Act: Move the third flight to before the second flight with conflict
        sequence.MoveFlight("GHI789", newLandingTime, ["34L"]);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "first flight should remain at its estimate");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "moved flight should be delayed behind first flight");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(_landingRate), "second flight should be delayed behind moved flight");

        // Verify ordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "first flight should be first in sequence");
        sequence.NumberInSequence(flight3).ShouldBe(2, "moved flight should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "second flight should be third in sequence");
    }

    [Fact]
    public void MoveFlight_ToAnotherRunway_RepositionsFlight_BasedOnTargetTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build(); // Uses default runway mode with 34L and 34R

        var flight1_34L = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2_34L = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .Build();

        var flight1_34R = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34R")
            .Build();

        var flight2_34R = new FlightBuilder("JKL012")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        sequence.Insert(flight1_34L, flight1_34L.LandingEstimate);
        sequence.Insert(flight2_34L, flight2_34L.LandingEstimate);
        sequence.Insert(flight1_34R, flight1_34R.LandingEstimate);
        sequence.Insert(flight2_34R, flight2_34R.LandingEstimate);

        var newLandingTime = _time.AddMinutes(8); // Between the two 34R flights

        // Act: Move the second flight on 34L to 34R
        sequence.MoveFlight("DEF456", newLandingTime, ["34R"]);

        // Assert
        flight1_34L.LandingTime.ShouldBe(flight1_34L.LandingEstimate, "first 34L flight should remain unchanged");
        flight1_34R.LandingTime.ShouldBe(flight1_34R.LandingEstimate, "first 34R flight should remain unchanged");

        flight2_34L.LandingTime.ShouldBe(newLandingTime, "moved flight should land at target time");
        flight2_34L.AssignedRunwayIdentifier.ShouldBe("34R", "moved flight should be assigned to 34R");

        flight2_34R.LandingTime.ShouldBe(flight2_34L.LandingTime.Add(_landingRate), "second 34R flight should be delayed behind moved flight");

        // Verify runway-specific ordering
        sequence.NumberForRunway(flight1_34L).ShouldBe(1, "first 34L flight should be #1 for 34L");
        sequence.NumberForRunway(flight1_34R).ShouldBe(1, "first 34R flight should be #1 for 34R");
        sequence.NumberForRunway(flight2_34L).ShouldBe(2, "moved flight should be #2 for 34R");
        sequence.NumberForRunway(flight2_34R).ShouldBe(3, "second 34R flight should be #3 for 34R");
    }

    [Fact]
    public void SwapFlights_RepositionsBothFlights()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithFeederFixTime(_time.AddMinutes(3))
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(8))
            .WithRunway("34L")
            .WithFeederFixTime(_time.AddMinutes(6))
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;
        var originalFlight1FeederFixTime = flight1.FeederFixTime;
        var originalFlight2FeederFixTime = flight2.FeederFixTime;

        // Act: Swap the two flights
        sequence.SwapFlights("ABC123", "DEF456");

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight2LandingTime, "first flight should have second flight's original landing time");
        flight2.LandingTime.ShouldBe(originalFlight1LandingTime, "second flight should have first flight's original landing time");

        flight1.FeederFixTime.ShouldBe(originalFlight2FeederFixTime, "first flight should have second flight's original feeder fix time");
        flight2.FeederFixTime.ShouldBe(originalFlight1FeederFixTime, "second flight should have first flight's original feeder fix time");

        // Verify ordering is swapped
        sequence.NumberInSequence(flight2).ShouldBe(1, "second flight should now be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "first flight should now be second in sequence");
    }

    [Fact]
    public void Reposition_WithNoConflict_MovesFlight_BasedOnTargetTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var superStableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(25))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(superStableFlight, superStableFlight.LandingEstimate);
        sequence.Insert(stableFlight, stableFlight.LandingEstimate);
        sequence.Insert(unstableFlight, unstableFlight.LandingEstimate);

        // Act: Change the estimate of the unstable flight to before the Stable flight
        unstableFlight.UpdateLandingEstimate(_time.AddMinutes(10));
        sequence.Reposition(unstableFlight, unstableFlight.LandingEstimate);

        // Assert
        superStableFlight.LandingTime.ShouldBe(superStableFlight.LandingEstimate, "SuperStable flight should remain unchanged");
        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate, "unstable flight should land at its new estimate");
        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate, "stable flight should remain unchanged");

        // Verify ordering
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "SuperStable flight should be first in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should now be second in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(3, "stable flight should now be third in sequence");
    }

    [Fact]
    public void Reposition_WithConflict_MovesFlight_BasedOnTargetTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var superStableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.SuperStable)
            .Build();

        var unstableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        sequence.Insert(superStableFlight, superStableFlight.LandingEstimate);
        sequence.Insert(unstableFlight, unstableFlight.LandingEstimate);
        sequence.Insert(stableFlight, stableFlight.LandingEstimate);

        // Act: Change the estimate of the unstable flight to just before the Stable flight (causing conflict)
        unstableFlight.UpdateLandingEstimate(_time.AddMinutes(18));
        sequence.Reposition(unstableFlight, unstableFlight.LandingEstimate);

        // Assert
        superStableFlight.LandingTime.ShouldBe(superStableFlight.LandingEstimate, "SuperStable flight should remain unchanged");
        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate, "stable flight should remain unchanged");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind the stable flight");

        // Verify ordering
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "SuperStable flight should be first in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should be second in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(3, "unstable flight should be third in sequence");
    }

    [Fact]
    public void Reposition_WithConflictWithUnstableFlights_MovesFlight_BasedOnTargetTime()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        // Act: Reverse the order of the estimates
        flight1.UpdateLandingEstimate(_time.AddMinutes(15));
        flight2.UpdateLandingEstimate(_time.AddMinutes(10));
        flight3.UpdateLandingEstimate(_time.AddMinutes(5));

        sequence.Reposition(flight1, flight1.LandingEstimate);
        sequence.Reposition(flight2, flight2.LandingEstimate);
        sequence.Reposition(flight3, flight3.LandingEstimate);

        // Assert that the flights are reordered accordingly
        flight3.LandingTime.ShouldBe(flight3.LandingEstimate, "flight3 should land at its new estimate");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "flight2 should land at its new estimate");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "flight1 should land at its new estimate");

        // Verify ordering is reversed
        sequence.NumberInSequence(flight3).ShouldBe(1, "flight3 should now be first in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(2, "flight2 should now be second in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(3, "flight1 should now be third in sequence");
    }

    [Fact]
    public void Reposition_MultipleUnstableFlights_InRunwayChangePeriod_MovesAfterRunwayChange()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        // Schedule a runway mode change in the near future
        var lastLandingTimeForOldMode = _time.AddMinutes(10);
        var firstLandingTimeForNewMode = _time.AddMinutes(20);
        var newRunwayMode = new RunwayMode(new RunwayModeConfiguration
        {
            Identifier = "34R",
            Runways = [new RunwayConfiguration { Identifier = "34R", LandingRateSeconds = (int)_landingRate.TotalSeconds, Dependencies = [new RunwayDependency {RunwayIdentifier = "34L"}]}]
        });

        sequence.ChangeRunwayMode(newRunwayMode, lastLandingTimeForOldMode, firstLandingTimeForNewMode);

        // Insert multiple unstable flights with estimates that would place them in the runway change period
        var flight1 = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(3))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(6))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithLandingEstimate(_time.AddMinutes(9))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);
        sequence.Insert(flight3, flight3.LandingEstimate);

        // Act: Change estimates to place them in the runway change period and reposition each flight
        flight1.UpdateLandingEstimate(_time.AddMinutes(13)); // In runway change period (8-12 minutes)
        flight2.UpdateLandingEstimate(_time.AddMinutes(16)); // In runway change period
        flight3.UpdateLandingEstimate(_time.AddMinutes(19)); // In runway change period

        sequence.Reposition(flight1, flight1.LandingEstimate);
        sequence.Reposition(flight2, flight2.LandingEstimate);
        sequence.Reposition(flight3, flight3.LandingEstimate);

        // Assert: Each flight should be moved to after the runway change period, maintaining proper separation
        flight1.LandingTime.ShouldBe(firstLandingTimeForNewMode, "first flight should be moved to the beginning of the new runway mode");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(_landingRate), "second flight should be delayed behind the first flight");
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(_landingRate), "third flight should be delayed behind the second flight");
    }

    SequenceBuilder GetSequenceBuilder() =>
        new SequenceBuilder(airportConfigurationFixture.Instance).WithClock(clockFixture.Instance);
}
