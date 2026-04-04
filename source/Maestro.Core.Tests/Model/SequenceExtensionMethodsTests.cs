using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class SequenceExtensionMethodsTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _time = clockFixture.Instance.UtcNow();
    readonly TimeSpan _landingRate = TimeSpan.FromSeconds(180);
    readonly TimeSpan _defaultTtg = TimeSpan.FromMinutes(20);

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

    [Fact]
    public void RepositionByEstimate_WithOnlyOneFlight_NoChangesAreMade()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build();

        var flight = new FlightBuilder("ABC123")
            .WithLandingEstimate(_time.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        sequence.Insert(0, flight);
        var originalLandingTime = flight.LandingTime;

        // Act: Reposition the only flight
        sequence.RepositionByLandingEstimate(flight);

        // Assert
        flight.LandingTime.ShouldBe(originalLandingTime, "flight's landing time should not change");
        sequence.NumberInSequence(flight).ShouldBe(1, "flight should still be first in sequence");
    }

    [Fact]
    public void RepositionByEstimate_WhenEtaIsEarlier_AndUnstableFlightIsInFront_PositionsAreSwapped()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
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
        sequence.RepositionByLandingEstimate(flight2);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

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
        sequence.RepositionByLandingEstimate(unstableFlight);

        // Assert: unstableFlight should remain in front and land at its new estimate, stable flight should be displaced
        sequence.NumberInSequence(unstableFlight).ShouldBe(1, "unstable flight should remain first in sequence");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should remain second in sequence");

        unstableFlight.LandingTime.ShouldBe(unstableFlight.LandingEstimate, "unstable flight should land at its new estimate");
        stableFlight.LandingTime.ShouldBe(unstableFlight.LandingTime.Add(_landingRate), "stable flight should be displaced to maintain separation");
        stableFlight.LandingTime.ShouldNotBe(originalStableLandingTime, "stable flight's landing time should have changed");
    }

    SequenceBuilder GetSequenceBuilder() =>
        new SequenceBuilder(CreateSingleRunwayConfiguration("34L", _landingRate)).WithClock(clockFixture.Instance);

    #region RepositionByFeederFixEstimate Tests

    [Fact]
    public void RepositionByFeederFixEstimate_WithOnlyOneFlight_NoChangesAreMade()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build();

        var flight = new FlightBuilder("ABC123")
            .WithFeederFixEstimate(_time.AddMinutes(5), TimeSpan.FromMinutes(20))
            .WithRunway("34L")
            .Build();

        sequence.Insert(0, flight);
        var originalLandingTime = flight.LandingTime;

        // Act: Reposition the only flight
        sequence.RepositionByFeederFixEstimate(flight);

        // Assert
        flight.LandingTime.ShouldBe(originalLandingTime, "flight's landing time should not change");
        sequence.NumberInSequence(flight).ShouldBe(1, "flight should still be first in sequence");
    }

    [Fact]
    public void RepositionByFeederFixEstimate_WhenFeederFixEtaIsEarlier_FlightsAreReordered()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build();

        // flight1 has earlier feeder fix ETA but LATER landing estimate due to longer TTG
        var flight1 = new FlightBuilder("ABC123")
            .WithFeederFixEstimate(_time.AddMinutes(5), TimeSpan.FromMinutes(25))  // Lands at _time + 30min
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        // flight2 has later feeder fix ETA but EARLIER landing estimate due to shorter TTG
        var flight2 = new FlightBuilder("DEF456")
            .WithFeederFixEstimate(_time.AddMinutes(10), TimeSpan.FromMinutes(15)) // Lands at _time + 25min
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Sanity check: flight1 should be first, flight2 second
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);

        // Verify landing estimates are in reverse order (proving different TTGs)
        flight2.LandingEstimate.ShouldBeLessThan(flight1.LandingEstimate,
            "flight2 should have earlier landing estimate despite later feeder fix estimate");

        // Act: Update flight2's feeder fix estimate to be earlier than flight1 and reposition
        flight2.UpdateFeederFixEstimate(_time.AddMinutes(3));
        sequence.RepositionByFeederFixEstimate(flight2);

        // Assert: Positions should be swapped based on feeder fix estimates
        sequence.NumberInSequence(flight2).ShouldBe(1, "flight2 should now be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "flight1 should now be second in sequence");

        // Verify landing times are scheduled correctly
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "flight2 should land at its estimate");
        flight1.LandingTime.ShouldBe(flight2.LandingTime.Add(_landingRate), "flight1 should be delayed behind flight2");
    }

    [Fact]
    public void RepositionByFeederFixEstimate_WithThreeFlights_PositionsCorrectlyInMiddle()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithFeederFixEstimate(_time.AddMinutes(5), TimeSpan.FromMinutes(30))  // Lands at _time + 35min
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithFeederFixEstimate(_time.AddMinutes(10), TimeSpan.FromMinutes(20)) // Lands at _time + 30min
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithFeederFixEstimate(_time.AddMinutes(20), TimeSpan.FromMinutes(10)) // Lands at _time + 30min
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);
        sequence.Insert(2, flight3);

        // Sanity check
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        // Act: Update flight3's feeder fix estimate to be between flight1 and flight2
        flight3.UpdateFeederFixEstimate(_time.AddMinutes(7));
        sequence.RepositionByFeederFixEstimate(flight3);

        // Assert: flight3 should now be positioned between flight1 and flight2
        sequence.NumberInSequence(flight1).ShouldBe(1, "flight1 should remain first");
        sequence.NumberInSequence(flight3).ShouldBe(2, "flight3 should now be second");
        sequence.NumberInSequence(flight2).ShouldBe(3, "flight2 should now be third");
    }

    [Fact]
    public void RepositionByFeederFixEstimate_WhenMovingToEnd_PositionsCorrectly()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build();

        var flight1 = new FlightBuilder("ABC123")
            .WithFeederFixEstimate(_time.AddMinutes(5), TimeSpan.FromMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithFeederFixEstimate(_time.AddMinutes(10), TimeSpan.FromMinutes(25)) // Later landing despite earlier feeder fix
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Sanity check
        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);

        // Act: Update flight1's feeder fix estimate to be later than flight2
        flight1.UpdateFeederFixEstimate(_time.AddMinutes(15));
        sequence.RepositionByFeederFixEstimate(flight1);

        // Assert: flight1 should now be positioned after flight2
        sequence.NumberInSequence(flight2).ShouldBe(1, "flight2 should now be first");
        sequence.NumberInSequence(flight1).ShouldBe(2, "flight1 should now be second");
    }

    [Fact]
    public void RepositionByFeederFixEstimate_WithDifferentTTGs_OrdersByFeederFixNotLandingEstimate()
    {
        // Arrange
        var sequence = GetSequenceBuilder()
            .Build();

        // Create flights where landing estimate order is OPPOSITE to feeder fix estimate order
        var flight1 = new FlightBuilder("ABC123")
            .WithFeederFixEstimate(_time.AddMinutes(5), TimeSpan.FromMinutes(35))  // Feeder: +5, Landing: +40
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("DEF456")
            .WithFeederFixEstimate(_time.AddMinutes(10), TimeSpan.FromMinutes(20)) // Feeder: +10, Landing: +30
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var flight3 = new FlightBuilder("GHI789")
            .WithFeederFixEstimate(_time.AddMinutes(15), TimeSpan.FromMinutes(5))  // Feeder: +15, Landing: +20
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);
        sequence.Insert(2, flight3);

        // Verify landing estimates are in REVERSE order of feeder fix estimates
        flight3.LandingEstimate.ShouldBeLessThan(flight2.LandingEstimate);
        flight2.LandingEstimate.ShouldBeLessThan(flight1.LandingEstimate);

        // But feeder fix estimates are in correct order
        flight1.FeederFixEstimate.ShouldBeLessThan(flight2.FeederFixEstimate);
        flight2.FeederFixEstimate.ShouldBeLessThan(flight3.FeederFixEstimate);

        // Act: Update flight3 to have earliest feeder fix estimate
        flight3.UpdateFeederFixEstimate(_time.AddMinutes(2));
        sequence.RepositionByFeederFixEstimate(flight3);

        // Assert: Order should be based on feeder fix estimates, not landing estimates
        sequence.NumberInSequence(flight3).ShouldBe(1, "flight3 should be first (earliest feeder fix)");
        sequence.NumberInSequence(flight1).ShouldBe(2, "flight1 should be second");
        sequence.NumberInSequence(flight2).ShouldBe(3, "flight2 should be third");
    }

    #endregion
}
