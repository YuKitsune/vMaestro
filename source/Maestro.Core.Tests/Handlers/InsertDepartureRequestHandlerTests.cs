using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class InsertDepartureRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly TimeSpan _arrivalInterval = TimeSpan.FromMinutes(16);

    [Fact]
    public async Task WhenFlightIsInserted_TheStateIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.State.ShouldBe(State.Stable, "flight state should be set to Stable when inserted");
    }

    [Fact]
    public async Task LandingEstimateIsDerivedFromTakeOffTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var estimatedEnrouteTime = TimeSpan.FromMinutes(45); // NonJet from YSCB per config
        var takeoffTime = now.AddMinutes(5);
        var expectedLandingEstimate = takeoffTime.Add(estimatedEnrouteTime);

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithAircraftCategory(AircraftCategory.NonJet) // Use NonJet to match 45-minute config
            .WithLandingEstimate(now.AddMinutes(60)) // Initial estimate, should be updated
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "DH8D", // Turboprop (NonJet) to match the 45-minute config
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(expectedLandingEstimate,
            "landing estimate should be calculated from takeoff time + EET");
    }

    [Fact]
    public async Task WhenFlightHasPosition_LandingEstimateIsNotDerivedFromTakeOffTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);
        var initialLandingEstimate = now.AddMinutes(10); // Much sooner than takeoff + EET (which would be ~35 minutes)

        // Create a flight position to simulate a radar-coupled flight
        var position = new FlightPosition(
            new Coordinate(1, 1), // Actual position doesn't matter
            15000,
            VerticalTrack.Descending,
            280,
            false);

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithAircraftCategory(AircraftCategory.Jet)
            .WithLandingEstimate(initialLandingEstimate)
            .WithState(State.Unstable)
            .WithPosition(position) // Set position to simulate radar-coupled flight
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(initialLandingEstimate,
            "landing estimate should not be recalculated when flight has a known position (radar-coupled)");
    }

    [Fact]
    public async Task WhenFlightHasPosition_ButIsOnGround_LandingEstimateDerivedFromTakeOffTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var estimatedEnrouteTime = TimeSpan.FromMinutes(30); // Jet from YSCB per config
        var takeoffTime = now.AddMinutes(5);
        var expectedLandingEstimate = takeoffTime.Add(estimatedEnrouteTime);
        var initialLandingEstimate = now.AddMinutes(60); // Fudged estimate due to inaccurate flight plan

        // Create a flight position to simulate a radar-coupled flight
        var position = new FlightPosition(
            new Coordinate(1, 1), // Actual position doesn't matter
            0,
            VerticalTrack.Maintaining,
            15,
            true);

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithAircraftCategory(AircraftCategory.Jet)
            .WithLandingEstimate(initialLandingEstimate)
            .WithState(State.Unstable)
            .WithPosition(position) // Set position to simulate radar-coupled flight
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingEstimate.ShouldBe(expectedLandingEstimate,
            "landing estimate should be calculated from takeoff time + EET");
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_ItIsInsertedBasedOnItsCalculatedLandingEstimate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var estimatedEnrouteTime = TimeSpan.FromMinutes(30);
        var takeoffTime = now.AddMinutes(5);
        var calculatedLandingEstimate = takeoffTime.Add(estimatedEnrouteTime); // now + 35 minutes

        // Create flights in sequence at T+20 and T+50
        var flight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(50))
            .WithLandingTime(now.AddMinutes(50))
            .WithFeederFixEstimate(now.AddMinutes(38))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(60))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // The flight should be inserted between flight1 (T+20) and flight2 (T+50) based on its estimate (T+35)
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence based on its landing estimate");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_RunwayIsAssignedByFeederPreference()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var estimatedEnrouteTime = TimeSpan.FromMinutes(30);
        var takeoffTime = now.AddMinutes(5);

        // Flight with BOREE feeder fix should prefer 34R runway (not 34L which is the default)
        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("BOREE")
            .WithLandingEstimate(now.AddMinutes(60))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // BOREE prefers 34R in the 34IVA runway mode (not the default 34L)
        pendingFlight.AssignedRunwayIdentifier.ShouldBe("34R",
            "runway should be assigned based on BOREE feeder fix preference for 34R");
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_RunwayIsAssignedByModeAtLandingEstimate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var estimatedEnrouteTime = TimeSpan.FromMinutes(30);
        var takeoffTime = now.AddMinutes(5);
        var calculatedLandingEstimate = takeoffTime.Add(estimatedEnrouteTime); // now + 35 minutes

        // Flight with BOREE feeder fix (prefers 34R in 34IVA mode, or 16L in 16IVA mode)
        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("BOREE")
            .WithLandingEstimate(now.AddMinutes(60))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        // Change runway mode to 16IVA starting at T+30 (before the landing estimate at T+35)
        sequence.ChangeRunwayMode(
            new RunwayMode(new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            })),
            now.AddMinutes(25),
            now.AddMinutes(30));

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert

        // BOREE prefers 16L in the 16IVA runway mode (not 16R)
        pendingFlight.AssignedRunwayIdentifier.ShouldBe("16L",
            "runway should be assigned based on BOREE feeder fix preference in 16IVA mode at landing estimate");
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_AndTheLandingEstimateIsBeforeAStableFlight_StableFlightIsDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var estimatedEnrouteTime = TimeSpan.FromMinutes(30); // Jet from YSCB per config
        var takeoffTime = now.AddMinutes(5);
        var calculatedLandingEstimate = takeoffTime.Add(estimatedEnrouteTime); // now + 35 minutes

        // Create two stable flights, one before (T+15) and one after (T+20) the calculated landing estimate
        var stableFlight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var stableFlight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(18))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(60))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(stableFlight1, stableFlight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // The landing order should be: stableFlight1, pendingFlight, stableFlight2
        sequence.NumberInSequence(stableFlight1).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence");
        sequence.NumberInSequence(stableFlight2).ShouldBe(3, "QFA789 should be third in sequence");

        // The first stable flight should be unaffected
        stableFlight1.LandingTime.ShouldBe(now.AddMinutes(15),
            "QFA456 should remain at its original landing time");

        // The second stable flight should be delayed to maintain separation with the inserted flight
        stableFlight2.LandingTime.ShouldBe(pendingFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA123");
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_AndTheLandingEstimateIsBeforeASuperStableFlight_InsertedFlightIsDelayed(State state)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeoffTime = now.AddMinutes(5);

        // Create a superstable/frozen flight at T+30 (after the calculated landing estimate at T+25)
        var superStableFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(35))
            .WithLandingTime(now.AddMinutes(35))
            .WithFeederFixEstimate(now.AddMinutes(20))
            .WithState(state)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(superStableFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(takeoffTime));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // The landing order should be: superStableFlight, pendingFlight
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence");

        // The superstable/frozen flight should be unaffected
        superStableFlight.LandingTime.ShouldBe(now.AddMinutes(35),
            "QFA456 should remain at its original landing time");

        // The inserted flight should be delayed and positioned after the superstable/frozen flight
        pendingFlight.LandingTime.ShouldBe(superStableFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA123 should be delayed to land after QFA456 with proper separation");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndItDoesNotExistInThePendingList_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA999",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act and Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA999 was not found in the pending list.");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndItIsNotFromDepartureAirport_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var pendingFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act and Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA123 is not from a departure airport.");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_ThePositionInTheSequenceIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_TheLandingTimeAndRunwayAreSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetLandingTime = now.AddMinutes(20);

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(targetLandingTime, ["34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingTime.ShouldBe(targetLandingTime, "landing time should be set to target time");

        // TODO: Assert that the runway is assigned to something in mode
        pendingFlight.AssignedRunwayIdentifier.ShouldBe("34R", "runway should be set to 34R");
        pendingFlight.RunwayManuallyAssigned.ShouldBe(true, "runway should be marked as manually assigned");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_ThePositionInTheSequenceIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_TheFlightIsInsertedBeforeTheReferenceFlightAndTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithFeederFixEstimate(now.AddMinutes(1))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA456 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");

        // The reference flight and trailing flights should be delayed
        flight1.LandingTime.ShouldBe(pendingFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA456 should be delayed to maintain separation behind QFA123");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_ThePositionInTheSequenceIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(existingFlight).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_TheFlightIsInsertedBehindTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(13))
            .WithFeederFixEstimate(now.AddMinutes(-1))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(11))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");

        // The inserted flight should be positioned behind the reference flight with proper separation
        pendingFlight.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA123 should be scheduled with separation behind QFA456 (the reference flight)");

        // Trailing flight should be delayed further
        flight2.LandingTime.ShouldBe(pendingFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA123");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BetweenTwoFrozenFlights_WithoutEnoughSpaceBetweenThem_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        // Two frozen flights with only 1x acceptance rate between them (need 2x)
        var frozenFlight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var frozenFlight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithFeederFixEstimate(now.AddMinutes(1))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(frozenFlight1, frozenFlight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("Cannot insert flight", Case.Insensitive);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(60))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new InsertDepartureRequestHandler(
            Substitute.For<IAirportConfigurationProvider>(),
            instanceManager,
            slaveConnectionManager,
            Substitute.For<IArrivalLookup>(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights.ShouldNotContain(pendingFlight, "Flight should not be inserted locally when relaying to master");
    }

    [Fact]
    public async Task WhenFlightIsInserted_ItIsRemovedFromThePendingList()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(60))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new DepartureInsertionOptions(now.AddMinutes(5)));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.PendingFlights.ShouldBeEmpty("Flight should be removed from pending list after insertion");
        sequence.Flights.ShouldContain(pendingFlight, "Flight should be in the sequence");
    }

    InsertDepartureRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence, IClock clock)
    {
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();
        return new InsertDepartureRequestHandler(
            airportConfigurationProvider,
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IArrivalLookup>(),
            clock,
            mediator,
            Substitute.For<ILogger>());
    }
}
