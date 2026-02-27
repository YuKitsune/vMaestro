using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
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
        var ttg = TimeSpan.FromMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(ttg)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var routeLandingTime = clock.UtcNow().AddMinutes(26); // 1 min off to ensure we're not sourcing landing ETA from the route

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", routeLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();
        trajectoryService.GetTrajectory(Arg.Any<Flight>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new Trajectory(ttg));

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldNotBe(routeLandingTime);
        flight.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItIsNotTrackingViaAFeederFix_EstimatesAreRecalculated()
    {
        // TODO: @claude, please implement this test case.

        // Arrange
        // TODO: Create a flight, not tracking via any feeder fix

        // Act
        // TODO: Update the landing estimate (last point in the route)

        // Assert
        // TODO: Assert the FeederFix has changed
        // TODO: Assert the FeederFix estimate is newLandingEstimate - Trajectory.TimeToGo
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItPassesTheFeederFix_PassedFeederFixTimeIsSet()
    {
        // TODO: @claude, please implement this test case.

        // Arrange
        // TODO: Create a flight

        // Act
        // TODO: Update the route estimate to include an ActualTime in the feeder fix

        // Assert
        // TODO: ActualFeederFixTime should now be set
        // TODO: Landing estimate is calculated based on ATO_FF + Trajectory.TimeToGo (Use a slightly different value, i.e. 1 minute off, for ETA_FF and ATO_FF to ensure ATO_FF is used and not ETA_FF)
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItIsNotTrackingViaAFeederFix_AndItPassesTheFeederFixPoint_PassedFeederFixTimeIsSet()
    {
        // TODO: @claude, please implement this test case.

        // Arrange
        // TODO: Create a flight, not tracking via any feeder fix

        // Act
        // TODO: Update the landing estimate to be now + Trajectory.TTG

        // Assert
        // TODO: ActualFeederFixTime should be set to newLandingEstimate - Trajectory.TTG
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItHasPassedTheFeederFix_EstimatesAreNoLongerUpdated()
    {
        // TODO: @claude, please implement this test case.

        // Arrange
        // TODO: Create a flight, with an ATO_FF set

        // Act
        // TODO: Update the ETA_FF and landing estimate (last ETA in route)

        // Assert
        // TODO: FeederFixEstimate and LandingEstimate should not change
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
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
            new FlightPosition(new Coordinate(1, 1), 38_000, VerticalTrack.Descending, 280, false), // Different position
            [new FixEstimate("WELSH", clock.UtcNow().AddMinutes(10))]);

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.AircraftType.ShouldBe("B744");
        flight.WakeCategory.ShouldBe(WakeCategory.Heavy);
        flight.OriginIdentifier.ShouldBe("YMAV");
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
        var ttg = TimeSpan.FromMinutes(10);
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(ttg)
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();
        instance.Session.DeSequencedFlights.Add(flight);

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var routeLandingTime = clock.UtcNow().AddMinutes(26); // 1 min off to ensure we're not sourcing landing ETA from the route

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", routeLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldNotBe(routeLandingTime);
        flight.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    [Fact]
    public async Task WhenAnUnstableFlightIsUpdated_ItsPositionInSequenceIsRecalculated()
    {
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate.
        //  Use different TTG values to make flight1 land earlier than flight2 based on landing time, but the different
        //  FeederFixEstimates should make flight2 overtake flight1.

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
            .WithState(State.Unstable)
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
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

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
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate. As with the previous test.

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
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

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
        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance).Build();

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
    public async Task WhenNewFlightFeederFixEstimateIsEarlierThanStableFlight_FlightIsInsertedBefore()
    {
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate. As with the previous tests.

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
    public async Task WhenNewFlightFeederFixEstimateIsEarlierThanSuperStableFlight_FlightIsInsertedAfter()
    {
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate. As with the previous tests.

        // Arrange
        var clock = clockFixture.Instance;

        var superStableFlight = new FlightBuilder("QFA456")
            .WithState(State.SuperStable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
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
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(5)), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(15))]);

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
    public async Task WhenUnstableFeederFixEstimateIsAheadOfStableFlight_ItDoesNotOvertakeStableFlight()
    {
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate. As with the previous tests.

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
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
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
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

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
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Flight should remain in desequenced list and estimates should be updated
        instance.Session.DeSequencedFlights.ShouldContain(flight, "flight should remain in desequenced list");
        sequence.Flights.ShouldBeEmpty("desequenced flight should NOT be added to the sequence");
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime, "desequenced flight estimates should be updated");
        flight.LandingEstimate.ShouldBe(newLandingTime, "desequenced flight estimates should be updated");
    }

    [Fact]
    public async Task WhenUnstableEstimateMovesBack_PositionIsCalculatedCorrectly()
    {
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate. As with the previous tests.

        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(5))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA789")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(15))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(25))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third initially");

        // Update QFA123 with a later estimate, moving it between QFA456 and QFA789
        var newFeederFixTime = clock.UtcNow().AddMinutes(12);
        var newLandingTime = clock.UtcNow().AddMinutes(22);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should move to position 2 (between QFA456 and QFA789)
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first after update");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second after moving forward in sequence");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third after update");
        flight1.LandingEstimate.ShouldBe(newLandingTime, "QFA123 estimate should be updated");
    }

    [Fact]
    public async Task WhenUnstableEstimateBecomesLatest_ItMovesToEndOfSequence()
    {
        // TODO: @claude please re-factor this test to ensure flights are positioned based on their FeederFix estimate,
        //  and not their landing estimate. As with the previous tests.

        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(5))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA789")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(15))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(25))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third initially");

        // Update QFA123 with a later estimate than all other flights
        var newFeederFixTime = clock.UtcNow().AddMinutes(20);
        var newLandingTime = clock.UtcNow().AddMinutes(30);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var trajectoryService = Substitute.For<ITrajectoryService>();

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should move to end of sequence
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first after update");
        sequence.NumberInSequence(flight3).ShouldBe(2, "QFA789 should be second after update");
        sequence.NumberInSequence(flight1).ShouldBe(3, "QFA123 should be last after moving to end with latest estimate");
        flight1.LandingEstimate.ShouldBe(newLandingTime, "QFA123 estimate should be updated");
    }

    FlightUpdatedHandler GetHandler(
        IMaestroInstanceManager instanceManager,
        IClock clock,
        IArrivalLookup? arrivalLookup = null,
        ITrajectoryService? trajectoryService = null,
        IFlightUpdateRateLimiter? rateLimiter = null,
        IMaestroConnectionManager? connectionManager = null)
    {
        rateLimiter ??= Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdateFlight(Arg.Any<Flight>()).Returns(true);

        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfigurationFixture.Instance]);

        arrivalLookup ??= Substitute.For<IArrivalLookup>();
        trajectoryService ??= Substitute.For<ITrajectoryService>();
        connectionManager ??= new MockLocalConnectionManager();
        var mediator = Substitute.For<IMediator>();

        return new FlightUpdatedHandler(
            instanceManager,
            connectionManager,
            rateLimiter,
            airportConfigurationProvider,
            arrivalLookup,
            trajectoryService,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
