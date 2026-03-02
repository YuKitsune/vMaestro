using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class SequenceExtensionMethodsTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly DateTimeOffset _time = clockFixture.Instance.UtcNow();
    readonly TimeSpan _landingRate = TimeSpan.FromSeconds(180);
    readonly TimeSpan _defaultTtg = TimeSpan.FromMinutes(20);

    [Fact]
    public void RepositionByEstimate_WithOnlyOneFlight_NoChangesAreMade()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var flight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        sequence.Insert(0, flight);
        var originalLandingTime = flight.LandingTime;

        // Act: Reposition the only flight
        sequence.RepositionByEstimate(flight);

        // Assert
        flight.LandingTime.ShouldBe(originalLandingTime, "flight's landing time should not change");
        sequence.NumberInSequence(flight).ShouldBe(1, "flight should still be first in sequence");
    }

    [Fact]
    public void RepositionByEstimate_WhenEtaIsEarlier_AndUnstableFlightIsInFront_PositionsAreSwapped()
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

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Sanity check: flight1 should be first, flight2 second
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);

        // Act: Update flight2's estimate to be earlier than flight1 and reposition
        flight2.UpdateFeederFixEstimate(_time.AddMinutes(3).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(flight2);

        // Assert: Positions should be swapped
        sequence.NumberInSequence(flight2).ShouldBe(1, "flight2 should now be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "flight1 should now be second in sequence");

        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "flight2 should land at its new estimate");
        flight1.LandingTime.ShouldBe(flight2.LandingTime.Add(_landingRate), "flight1 should be delayed behind flight2");
    }

    [Theory(Skip = "Inaccurate behaviour")]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void RepositionByEstimate_WhenEtaIsEarlier_AndStableFlightIsInFront_AndNewEtaConflicts_RepositionedFlightIsMovedBehindStableFlight(State stableFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var stableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        var unstableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, stableFlight);
        sequence.Insert(1, unstableFlight);

        // Sanity check
        sequence.NumberInSequence(stableFlight).ShouldBe(1);
        sequence.NumberInSequence(unstableFlight).ShouldBe(2);

        // Act: Update unstableFlight's estimate to be earlier than stableFlight but close enough to conflict (within landing rate)
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(4).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should be moved behind the stable flight due to conflict
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should remain first in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should remain second in sequence");

        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate, "stable flight should remain at its original time");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind stable flight");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void RepositionByEstimate_WhenEtaIsEarlier_AndStableFlightIsInFront_AndNewEtaDoesNotConflict_RepositionedFlightIsMovedInFrontOfStableFlight(State stableFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var stableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        var unstableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, stableFlight);
        sequence.Insert(1, unstableFlight);

        // Sanity check
        sequence.NumberInSequence(stableFlight).ShouldBe(1);
        sequence.NumberInSequence(unstableFlight).ShouldBe(2);

        // Act: Update unstableFlight's estimate to be earlier than stableFlight with enough separation (no conflict)
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(5).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should be moved in front of the stable flight
        sequence.NumberInSequence(unstableFlight).ShouldBe(1, "unstable flight should now be first in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should now be second in sequence");

        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate, "unstable flight should land at its new estimate");
        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate, "stable flight should remain at its original time");
    }

    [Theory(Skip = "Inaccurate behaviour")]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void RepositionByEstimate_WhenEtaIsLater_AndStableFlightIsBehind_AndNewEtaConflicts_RepositionedFlightIsMovedBehindStableFlight(State stableFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        sequence.Insert(0, unstableFlight);
        sequence.Insert(1, stableFlight);

        // Sanity check
        sequence.NumberInSequence(unstableFlight).ShouldBe(1);
        sequence.NumberInSequence(stableFlight).ShouldBe(2);

        // Act: Update unstableFlight's estimate to be 2 minutes before the stable flight (causing conflict with 3-minute spacing)
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(7).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should be moved behind the stable flight due to conflict
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should now be first in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should now be second in sequence");

        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate, "stable flight should remain at its original time");
        unstableFlight.LandingTime.ShouldBe(stableFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind stable flight");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void RepositionByEstimate_WhenEtaIsLater_AndStableFlightIsBehind_AndNewEtaDoesNotConflict_RepositionedFlightRemainsInFrontOfStableFlight(State stableFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        sequence.Insert(0, unstableFlight);
        sequence.Insert(1, stableFlight);

        // Sanity check
        sequence.NumberInSequence(unstableFlight).ShouldBe(1);
        sequence.NumberInSequence(stableFlight).ShouldBe(2);

        // Act: Update unstableFlight's estimate to be later but still with enough separation (no conflict)
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(6).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should remain in front of the stable flight
        sequence.NumberInSequence(unstableFlight).ShouldBe(1, "unstable flight should remain first in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should remain second in sequence");

        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate, "unstable flight should land at its new estimate");
        stableFlight.LandingTime.ShouldBe(stableFlight.LandingEstimate, "stable flight should remain at its original time");
    }

    [Fact]
    public void RepositionByEstimate_WhenNewEtaConflictsWithFrozenFlight_RepositionedFlightIsMovedBehindFrozenFlight()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var frozenFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var unstableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, frozenFlight);
        sequence.Insert(1, unstableFlight);

        // Freeze the flight after insertion so it gets scheduled properly
        frozenFlight.SetState(State.Frozen, clockFixture.Instance);

        // Sanity check
        sequence.NumberInSequence(frozenFlight).ShouldBe(1);
        sequence.NumberInSequence(unstableFlight).ShouldBe(2);

        // Act: Update unstableFlight's estimate to conflict with the frozen flight
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(9).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should be moved behind the frozen flight
        sequence.NumberInSequence(frozenFlight).ShouldBe(1, "frozen flight should remain first in sequence");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should remain second in sequence");

        frozenFlight.LandingTime.ShouldBe(frozenFlight.LandingEstimate, "frozen flight should remain at its scheduled time");
        unstableFlight.LandingTime.ShouldBe(frozenFlight.LandingTime.Add(_landingRate), "unstable flight should be delayed behind frozen flight");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void RepositionByEstimate_WhenEtaIsEarlier_AndStableFlightIsInFront_AndNewEtaConflicts_WithDisplaceStableFlights_StableFlightIsDisplaced(State stableFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var stableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        var unstableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, stableFlight);
        sequence.Insert(1, unstableFlight);

        // Sanity check
        sequence.NumberInSequence(stableFlight).ShouldBe(1);
        sequence.NumberInSequence(unstableFlight).ShouldBe(2);

        var originalStableLandingTime = stableFlight.LandingTime;

        // Act: Update unstableFlight's estimate to be earlier than stableFlight but close enough to conflict (within landing rate)
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(4).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should be moved in front and land at its estimate, stable flight should be displaced
        sequence.NumberInSequence(unstableFlight).ShouldBe(1, "unstable flight should now be first in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should now be second in sequence");

        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate, "unstable flight should land at its new estimate");
        stableFlight.LandingTime.ShouldBe(unstableFlight.LandingTime.Add(_landingRate), "stable flight should be displaced behind the unstable flight");
        stableFlight.LandingTime.ShouldNotBe(originalStableLandingTime, "stable flight's landing time should have changed");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public void RepositionByEstimate_WhenEtaIsLater_AndStableFlightIsBehind_AndNewEtaConflicts_WithDisplaceStableFlights_UnstableFlightRemainsInFrontAndStableFlightIsDisplaced(State stableFlightState)
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .WithSingleRunway("34L", _landingRate)
            .Build();

        var unstableFlight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var stableFlight = new FlightBuilder("DEF456")
            .WithLandingEstimate(_time.AddMinutes(10))
            .WithRunway("34L")
            .WithState(stableFlightState)
            .Build();

        sequence.Insert(0, unstableFlight);
        sequence.Insert(1, stableFlight);

        // Sanity check
        sequence.NumberInSequence(unstableFlight).ShouldBe(1);
        sequence.NumberInSequence(stableFlight).ShouldBe(2);

        var originalStableLandingTime = stableFlight.LandingTime;

        // Act: Update unstableFlight's estimate to be 2 minutes before the stable flight (causing conflict with 3-minute spacing)
        unstableFlight.UpdateFeederFixEstimate(_time.AddMinutes(8).Subtract(_defaultTtg));
        sequence.RepositionByEstimate(unstableFlight);

        // Assert: unstableFlight should remain in front and land at its new estimate, stable flight should be displaced
        sequence.NumberInSequence(unstableFlight).ShouldBe(1, "unstable flight should remain first in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should remain second in sequence");

        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate, "unstable flight should land at its new estimate");
        stableFlight.LandingTime.ShouldBe(unstableFlight.LandingTime.Add(_landingRate), "stable flight should be displaced to maintain separation");
        stableFlight.LandingTime.ShouldNotBe(originalStableLandingTime, "stable flight's landing time should have changed");
    }

    SequenceBuilder GetSequenceBuilder() =>
        new SequenceBuilder(airportConfigurationFixture.Instance).WithClock(clockFixture.Instance);
}
