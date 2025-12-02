using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightUpdatedHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly FlightPosition _position = new(
        new Coordinate(0, 0),
        0,
        VerticalTrack.Maintaining,
        0,
        false);

    [Fact]
    public async Task WhenAFlightIsOutOfRangeOfFeederFix_ItShouldNotBeTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(4),
            "RIVET4",
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenAFlightIsInRangeOfFeederFix_ItShouldBeTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Unstable);
    }

    [Fact]
    public async Task WhenAFlightIsOnGroundAtDepartureAirport_ItShouldBeAddedToThePendingList()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var position = new FlightPosition(
            new Coordinate(0, 0),
            0,
            VerticalTrack.Maintaining,
            0,
            true);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YSCB", // Departure airport configured in fixture
            "YSSY",
            clock.UtcNow().AddMinutes(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        var flight = instance.Session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe(notification.Callsign);
    }

    [Fact]
    public async Task WhenAFlightIsUncoupledAtDepartureAirport_ItShouldBeToThePendingList()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YSCB", // Departure airport configured in fixture
            "YSSY",
            clock.UtcNow().AddMinutes(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        var flight = instance.Session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe(notification.Callsign);
    }

    [Fact]
    public async Task WhenAFlightIsOnGroundAtNonDepartureAirport_ItShouldNotBeTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var position = new FlightPosition(
            new Coordinate(0, 0),
            0,
            VerticalTrack.Maintaining,
            0,
            true);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YXXX", // Non-departure airport
            "YSSY",
            clock.UtcNow().AddHours(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAnExistingFlightIsUpdated_ItsEstimatesAreRecalculated(State state)
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldBe(newLandingTime);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_ButNoPositionIsAvailable_EstimatesAreNotRecalculated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var originalFeederFixTime = clock.UtcNow().AddMinutes(10);
        var originalLandingTime = clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixTime)
            .WithLandingEstimate(originalLandingTime)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddHours(1)),
                new FixEstimate("YSSY", clock.UtcNow().AddHours(1.25))
            ]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixTime);
        flight.LandingEstimate.ShouldBe(originalLandingTime);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndTheFeederFixEstimateWasManuallyAssigned_EstimatesAreNotUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var manualFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var manualLandingEstimate = clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(manualFeederFixEstimate, manual: true)
            .WithLandingEstimate(manualLandingEstimate)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))
            ]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(manualFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(manualLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AllFlightDataIsUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B744", // Different aircraft type
            AircraftCategory.Jet,
            WakeCategory.Heavy, // Different wake category
            "YMAV", // Different origin
            "YSSY",
            clock.UtcNow().AddHours(-1.5), // Different estimated departure
            TimeSpan.FromHours(2),
            "ODALE7", // Different arrival
            new FlightPosition(new Coordinate(1, 1), 38_000, VerticalTrack.Descending, 280, false), // Different position
            [new FixEstimate("WELSH", clock.UtcNow().AddMinutes(10))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.AircraftType.ShouldBe("B744");
        flight.WakeCategory.ShouldBe(WakeCategory.Heavy);
        flight.OriginIdentifier.ShouldBe("YMAV");
        flight.EstimatedDepartureTime.ShouldBe(notification.EstimatedDepartureTime);
        flight.AssignedArrivalIdentifier.ShouldBe("ODALE7");
        flight.Position!.Coordinate.Latitude.ShouldBe(notification.Position!.Coordinate.Latitude);
        flight.Position.Coordinate.Longitude.ShouldBe(notification.Position.Coordinate.Longitude);
        flight.Position.Altitude.ShouldBe(notification.Position.Altitude);
        flight.Position.GroundSpeed.ShouldBe(notification.Position.GroundSpeed);
        flight.Fixes.ShouldHaveSingleItem().ShouldBe(notification.Estimates.Single());
        flight.LastSeen.ShouldBe(clock.UtcNow());
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_ItsEstimatesAreStillUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();
        instance.Session.DeSequencedFlights.Add(flight);

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldBe(newLandingTime);
    }

    [Fact]
    public async Task WhenAnUnstableFlightIsUpdated_ItsPositionInSequenceIsRecalculated()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight2, flight1)) // QFA456 first (earlier estimate)
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first initially");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second initially");

        // Update QFA123 with an earlier landing estimate
        var newFeederFixTime = clock.UtcNow().AddMinutes(5);
        var newLandingTime = clock.UtcNow().AddMinutes(15);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should now be first due to earlier estimate
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first after update with earlier estimate");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second after QFA123 moves ahead");
        flight1.LandingEstimate.ShouldBe(newLandingTime, "QFA123 estimate should be updated");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAStableFlightIsUpdated_ItsPositionInSequenceIsNotRecalculated(State state)
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Frozen)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight2, flight1)) // QFA456 first (earlier estimate)
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first initially");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second initially");

        // Update QFA123 with an earlier landing estimate (this would normally move it ahead)
        var newFeederFixTime = clock.UtcNow().AddMinutes(5);
        var newLandingTime = clock.UtcNow().AddMinutes(15);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Position should remain unchanged for stable flights
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should remain first - stable flights don't get repositioned");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should remain second - stable flights don't get repositioned");

        // But estimates should still be updated
        if (state != State.Landed)
        {
            flight1.LandingEstimate.ShouldBe(newLandingTime, "estimates should still be updated for non-landed flights");
            flight1.FeederFixEstimate.ShouldBe(newFeederFixTime, "estimates should still be updated for non-landed flights");
        }
    }

    [Fact]
    public async Task WhenNoInstancesAreActiveForDestination_FlightIsNotTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YBBN", // Destination with no active instance
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        instanceManager.InstanceExists("YBBN").ShouldBeFalse("no instance should exist for YBBN");
    }

    [Fact]
    public async Task WhenFlightIsNotTrackingViaKnownFeederFix_ItIsAddedToThePendingList()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        // Notification with no feeder fix estimates (only landing estimate)
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            null,
            _position,
            [new FixEstimate("YSSY", clock.UtcNow().AddMinutes(30))]); // Only landing estimate, no feeder fix

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = instance.Session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe("QFA123");
        flight.HighPriority.ShouldBeTrue("flights without feeder fix should be high priority");
    }

    [Fact]
    public async Task WhenFlightIsNotCurrentlyTracked_RunwayIsAssignedBasedOnFeederFixPreferences()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        // Use BOREE feeder fix which prefers 34R (not the default 34L)
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "BOREE4",
            _position,
            [new FixEstimate("BOREE", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.AssignedRunwayIdentifier.ShouldBe("34R", "BOREE feeder fix should prefer runway 34R");
    }

    [Fact]
    public async Task WhenNewFlightLandingEstimateIsEarlierThanStableFlight_FlightIsInsertedBefore()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var stableFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(stableFlight))
            .Build();

        // New flight with earlier landing estimate than stable flight
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should be inserted before the stable flight");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should now be second");
    }

    [Fact]
    public async Task WhenNewFlightLandingEstimateIsEarlierThanSuperStableFlight_FlightIsInsertedAfter()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var superStableFlight = new FlightBuilder("QFA456")
            .WithState(State.SuperStable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(superStableFlight))
            .Build();

        // New flight with later landing estimate than superstable flight
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "superstable flight should remain first");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight should be inserted after the superstable flight");
    }

    [Fact]
    public async Task WhenUnstableFlightEstimateIsAheadOfStableFlight_ItDoesNotOvertakeStableFlight()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var stableFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var unstableFlight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(25))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(35))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(stableFlight, unstableFlight))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should be first initially");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should be second initially");

        // Update unstable flight with earlier estimate that would normally move it ahead
        var newFeederFixTime = clock.UtcNow().AddMinutes(10);
        var newLandingTime = clock.UtcNow().AddMinutes(20);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Unstable should NOT overtake stable flight
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should remain first - unstable flights cannot overtake stable flights");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should remain second - cannot overtake stable flight");
    }

    [Fact]
    public async Task WhenLastUpdateWasRecent_TheUpdateIsIgnored()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var originalEstimate = clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalEstimate)
            .WithLandingEstimate(originalEstimate.AddMinutes(10))
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))
            ]);

        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdateFlight(flight).Returns(false); // Rate limit triggered

        var handler = GetHandler(instanceManager, clock, rateLimiter: rateLimiter);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Estimates should not change
        flight.FeederFixEstimate.ShouldBe(originalEstimate, "estimate should not update when rate limited");
    }

    [Fact]
    public async Task WhenInSlaveMode_UpdateIsRelayedToMaster()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var handler = GetHandler(instanceManager, clock, connectionManager: slaveConnectionManager);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedNotifications.Count.ShouldBe(1, "notification should be relayed to master");
        slaveConnectionManager.Connection.InvokedNotifications[0].ShouldBe(notification, "the relayed notification should match the original");
        sequence.Flights.ShouldBeEmpty("flight should not be created locally when relaying to master");
    }

    [Fact]
    public async Task WhenAPendingFlightIsUpdated_ItShouldNotBeAddedToTheSequence()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();
        instance.Session.PendingFlights.Add(flight);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))
            ]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Flight should remain in pending list only
        instance.Session.PendingFlights.ShouldContain(flight, "flight should remain in pending list");
        sequence.Flights.ShouldBeEmpty("pending flight should NOT be added to the sequence");
        flight.LastSeen.ShouldBe(clock.UtcNow(), "flight last seen should be updated");
    }

    [Fact]
    public async Task WhenAPendingUnstableFlightIsUpdated_EstimatesAreNotCalculated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var originalFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var originalLandingEstimate = clock.UtcNow().AddMinutes(20);

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixEstimate)
            .WithLandingEstimate(originalLandingEstimate)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();
        instance.Session.PendingFlights.Add(flight);

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Estimates should NOT be updated for pending flights
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate, "pending flight estimates should not be updated");
        flight.LandingEstimate.ShouldBe(originalLandingEstimate, "pending flight estimates should not be updated");
        instance.Session.PendingFlights.ShouldContain(flight, "flight should remain in pending list");
        sequence.Flights.ShouldBeEmpty("pending flight should not be in the sequence");
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_ItShouldNotBeAddedToTheSequence()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();
        instance.Session.DeSequencedFlights.Add(flight);

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Flight should remain in desequenced list and estimates should be updated
        instance.Session.DeSequencedFlights.ShouldContain(flight, "flight should remain in desequenced list");
        sequence.Flights.ShouldBeEmpty("desequenced flight should NOT be added to the sequence");
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime, "desequenced flight estimates should be updated");
        flight.LandingEstimate.ShouldBe(newLandingTime, "desequenced flight estimates should be updated");
    }

    [Fact]
    public async Task WhenASequencedUnstableFlightIsUpdated_ItShouldBeRepositioned()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight2, flight1))
            .Build();

        // Verify initial state
        sequence.Flights.ShouldContain(flight1, "QFA123 should be in the sequence");
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first initially");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second initially");

        var newFeederFixTime = clock.UtcNow().AddMinutes(5);
        var newLandingTime = clock.UtcNow().AddMinutes(15);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(instanceManager, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should be repositioned ahead of QFA456
        sequence.Flights.ShouldContain(flight1, "QFA123 should remain in the sequence");
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be repositioned to first with earlier estimate");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should now be second");
        flight1.LandingEstimate.ShouldBe(newLandingTime, "estimates should be updated");
    }

    FlightUpdatedHandler GetHandler(
        IMaestroInstanceManager instanceManager,
        IClock clock,
        IEstimateProvider? estimateProvider = null,
        IFlightUpdateRateLimiter? rateLimiter = null,
        IMaestroConnectionManager? connectionManager = null)
    {
        rateLimiter ??= Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdateFlight(Arg.Any<Flight>()).Returns(true);

        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfigurationFixture.Instance]);

        estimateProvider ??= Substitute.For<IEstimateProvider>();
        connectionManager ??= new MockLocalConnectionManager();
        var mediator = Substitute.For<IMediator>();

        return new FlightUpdatedHandler(
            instanceManager,
            connectionManager,
            rateLimiter,
            airportConfigurationProvider,
            estimateProvider,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
