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
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(50))
            ]);

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
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(50))
            ]);

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
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(50))
            ]);

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
            .WithTrajectory(new Trajectory(ttg))
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
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
        // Arrange
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        // Create a flight not tracking via any feeder fix
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix(null)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(ttg))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithFlight(flight))
            .Build();

        // Update the landing estimate (last point in the route)
        var newLandingEstimate = clock.UtcNow().AddMinutes(25);

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
            [new FixEstimate("YSSY", newLandingEstimate)]); // Only destination, no feeder fix

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newLandingEstimate.Subtract(ttg));
        flight.LandingEstimate.ShouldBe(newLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItPassesTheFeederFix_PassedFeederFixTimeIsSet()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(ttg))
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        // Update the route estimate to include an ActualTime in the feeder fix
        var actualFeederFixTime = clock.UtcNow().AddMinutes(12);
        var estimatedFeederFixTime = clock.UtcNow().AddMinutes(13); // 1 minute off from actual

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
                new FixEstimate("RIVET", estimatedFeederFixTime, actualFeederFixTime),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(30))
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.ActualFeederFixTime.ShouldBe(actualFeederFixTime);
        flight.LandingEstimate.ShouldBe(actualFeederFixTime.Add(ttg));
        flight.LandingEstimate.ShouldNotBe(estimatedFeederFixTime.Add(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItIsNotTrackingViaAFeederFix_AndItPassesTheFeederFixPoint_PassedFeederFixTimeIsSet()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        // Create a flight not tracking via any feeder fix
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix(null)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(ttg))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithFlight(flight))
            .Build();

        // Update the landing estimate to be now + Trajectory.TTG
        // This means the calculated feeder fix time is "now" (flight has passed the feeder fix point)
        var newLandingEstimate = clock.UtcNow().Add(ttg);

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
            [new FixEstimate("YSSY", newLandingEstimate)]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.ActualFeederFixTime.ShouldBe(newLandingEstimate.Subtract(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItHasPassedTheFeederFix_EstimatesAreNoLongerUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        // Create a flight with an ATO_FF set (has already passed the feeder fix)
        var actualFeederFixTime = clock.UtcNow().AddMinutes(-5);
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(-5))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(5))
            .WithTrajectory(new Trajectory(ttg))
            .PassedFeederFixAt(actualFeederFixTime)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var originalFeederFixEstimate = flight.FeederFixEstimate;
        var originalLandingEstimate = flight.LandingEstimate;

        // Update the ETA_FF and landing estimate (last ETA in route)
        var newFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var newLandingEstimate = clock.UtcNow().AddMinutes(20);

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
                new FixEstimate("RIVET", newFeederFixEstimate, actualFeederFixTime),
                new FixEstimate("YSSY", newLandingEstimate)
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(originalLandingEstimate);
        flight.FeederFixEstimate.ShouldNotBe(newFeederFixEstimate);
        flight.LandingEstimate.ShouldNotBe(newLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_ButNoPositionIsAvailable_EstimatesAreNotRecalculated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var originalFeederFixTime = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixTime)
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
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndTheFeederFixEstimateWasManuallyAssigned_EstimatesAreNotUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var manualFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(manualFeederFixEstimate, manual: true)
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
            .WithTrajectory(new Trajectory(ttg))
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

        var handler = GetHandler(instanceManager, clock);

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
        // Arrange
        var clock = clockFixture.Instance;

        // Use different TTG values to prove positioning is based on FeederFixEstimate, not LandingEstimate
        // flight1: FF=+20, TTG=10, Landing=+30
        // flight2: FF=+10, TTG=22, Landing=+32
        // If positioned by FF: flight2 first (10 < 20)
        // If positioned by Landing: flight1 first (30 < 32)
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(22)))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService()
            .WithTrajectoryForFlight(flight1, new Trajectory(TimeSpan.FromMinutes(10)))
            .WithTrajectoryForFlight(flight2, new Trajectory(TimeSpan.FromMinutes(22)));

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight2, flight1)) // QFA456 first (earlier FF estimate)
            .Build();

        // Verify initial order, should be positioned by FeederFixEstimate (not LandingEstimate)
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first (earlier FF estimate)");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second (later FF estimate)");
        flight1.LandingEstimate.ShouldBeLessThan(flight2.LandingEstimate, "QFA123 lands earlier, but is positioned later due to FF estimate");

        // Update QFA123 with an earlier FeederFixEstimate
        var newFeederFixTime = clock.UtcNow().AddMinutes(5);

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
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(20))
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        // QFA123 should now be first due to earlier FeederFixEstimate
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first after update (earlier FF estimate)");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second (later FF estimate)");
        flight1.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight1.LandingEstimate.ShouldBe(newFeederFixTime.Add(TimeSpan.FromMinutes(10)));
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAStableFlightIsUpdated_ItsPositionInSequenceIsNotRecalculated(State state)
    {
        // Arrange
        var clock = clockFixture.Instance;

        // Sequences are ordered by landing time
        // flight1: FF=+10, TTG=10, Landing=+20 (Frozen, immovable)
        // flight2: FF=+20, TTG=10, Landing=+30 (state, Stable/SuperStable/Frozen)
        // Initial order by landing time: flight1 first (+20), flight2 second (+30)
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Frozen)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService()
            .WithTrajectoryForFlight(flight1, new Trajectory(TimeSpan.FromMinutes(10)))
            .WithTrajectoryForFlight(flight2, new Trajectory(TimeSpan.FromMinutes(10)));

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial order by landing time
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first (earlier landing time)");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second (later landing time)");

        // Update QFA456 with an earlier FeederFixEstimate (would normally move it ahead if unstable)
        // New: FF=+5, TTG=10, Landing=+15
        // If QFA456 were unstable, it would overtake QFA123 (15 < 20)
        var newFeederFixTime = clock.UtcNow().AddMinutes(5);

        var notification = new FlightUpdatedNotification(
            "QFA456",
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
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(15))
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        // Position should remain unchanged for stable flights even though landing time would cause reordering
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should remain first");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should remain second, as stable flights don't get repositioned");

        // But estimates should still be updated (except for Landed flights)
        if (state == State.Landed)
            return;

        flight2.FeederFixEstimate.ShouldBe(newFeederFixTime, "estimates should be updated even for stable flights");
        flight2.LandingEstimate.ShouldBe(clock.UtcNow().AddMinutes(15), "landing estimate should be updated even for stable flights");
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
        // Arrange
        var clock = clockFixture.Instance;

        // Use different TTG values to prove positioning is based on FeederFixEstimate, not LandingEstimate
        // stableFlight: FF=+20, TTG=10, Landing=+30
        // newFlight: FF=+15, TTG=18, Landing=+33
        // If positioned by FF: newFlight first (15 < 20)
        // If positioned by Landing: stableFlight first (30 < 33)
        var stableFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(TimeSpan.FromMinutes(18))
            .WithTrajectoryForFlight(stableFlight, new Trajectory(TimeSpan.FromMinutes(10)));

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(stableFlight))
            .Build();

        // New flight with earlier FeederFixEstimate (but later LandingEstimate due to longer TTG)
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

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should be inserted before stable flight (earlier FF estimate)");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should now be second (later FF estimate)");
        newFlight.LandingEstimate.ShouldBeGreaterThan(stableFlight.LandingEstimate, "new flight lands later, but is positioned earlier due to FF estimate");
    }

    [Fact]
    public async Task WhenNewFlightFeederFixEstimateIsEarlierThanSuperStableFlight_FlightIsInsertedAfter()
    {
        // Arrange
        var clock = clockFixture.Instance;

        // Use different TTG values to prove positioning respects SuperStable flight precedence
        // superStableFlight: FF=+10, TTG=10, Landing=+20
        // newFlight: FF=+5, TTG=18, Landing=+23
        // If positioned by FF alone: newFlight would be first (5 < 10) - but this doesn't happen!
        // Actual: newFlight inserted AFTER because SuperStable flights cannot be overtaken
        var superStableFlight = new FlightBuilder("QFA456")
            .WithState(State.SuperStable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlight(superStableFlight))
            .Build();

        // New flight with earlier FeederFixEstimate but later LandingEstimate
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
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(5))]);

        var trajectoryService = new MockTrajectoryService(TimeSpan.FromMinutes(18));
        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "superstable flight should remain first (cannot be overtaken)");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight inserted after superstable flight despite earlier FF estimate");
        newFlight.FeederFixEstimate.ShouldBeLessThan(superStableFlight.FeederFixEstimate, "new flight has earlier FF estimate but still positioned after");
    }

    [Fact]
    public async Task WhenUnstableFeederFixEstimateIsAheadOfStableFlight_ItDoesNotOvertakeStableFlight()
    {
        // Arrange
        var clock = clockFixture.Instance;

        // Use different TTG values to prove positioning respects stable flight precedence
        // stableFlight: FF=+20, TTG=10, Landing=+30
        // unstableFlight initial: FF=+25, TTG=18, Landing=+43
        // unstableFlight after update: FF=+10, TTG=10, Landing=+20
        // If positioned by FF: unstableFlight would be first after update (10 < 20)
        // If positioned by Landing: unstableFlight would be first after update (20 < 30)
        // Actual: stableFlight remains first (unstable cannot overtake stable)
        var stableFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var unstableFlight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(25))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(18)))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(stableFlight, unstableFlight))
            .Build();

        // Verify initial order
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should be first initially");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should be second initially");

        // Update unstable flight with earlier FeederFixEstimate (would move it ahead if both were unstable)
        var newFeederFixTime = clock.UtcNow().AddMinutes(10);
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

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
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Unstable should NOT overtake stable flight
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should remain first (unstable cannot overtake)");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should remain second (cannot overtake stable)");
        unstableFlight.FeederFixEstimate.ShouldBeLessThan(stableFlight.FeederFixEstimate, "unstable has earlier FF estimate but cannot overtake");
        unstableFlight.LandingEstimate.ShouldBeLessThan(stableFlight.LandingEstimate, "unstable would land earlier but cannot overtake");
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

        // Assert
        // Estimates should not change
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

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixEstimate)
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

        var handler = GetHandler(instanceManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        // Estimates should NOT be updated for pending flights
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate, "pending flight estimates should not be updated");
        instance.Session.PendingFlights.ShouldContain(flight, "flight should remain in pending list");
        sequence.Flights.ShouldBeEmpty("pending flight should not be in the sequence");
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_ItShouldNotBeAddedToTheSequence()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new Trajectory(ttg))
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

        var handler = GetHandler(instanceManager, clock);

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
        // Arrange
        var clock = clockFixture.Instance;

        // Use different TTG values to prove positioning is based on FeederFixEstimate, not LandingEstimate
        // flight1: FF=+5, TTG=10, Landing=+15
        // flight2: FF=+10, TTG=18, Landing=+28
        // flight3: FF=+15, TTG=12, Landing=+27
        // Initial order by FF: flight1, flight2, flight3
        // If ordered by Landing: flight1, flight3, flight2 (different!)
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(5))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(18)))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA789")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(15))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(12)))
            .WithRunway("34L")
            .Build();

        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg)
            .WithTrajectoryForFlight(flight1, new Trajectory(TimeSpan.FromMinutes(10)))
            .WithTrajectoryForFlight(flight2, new Trajectory(TimeSpan.FromMinutes(18)))
            .WithTrajectoryForFlight(flight3, new Trajectory(TimeSpan.FromMinutes(12)));

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order (positioned by FeederFixEstimate)
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially (earliest FF)");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third initially (latest FF)");
        flight3.LandingEstimate.ShouldBeLessThan(flight2.LandingEstimate, "flight3 lands earlier than flight2, but positioned later due to FF");

        // Update QFA123 with a later FeederFixEstimate, moving it between QFA456 and QFA789
        var newFeederFixTime = clock.UtcNow().AddMinutes(12);

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
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should move to position 2 (between QFA456 and QFA789 based on FF estimate)
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first after update (FF=+10)");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second after moving back (FF=+12)");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third after update (FF=+15)");
        flight1.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight1.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    [Fact]
    public async Task WhenUnstableEstimateBecomesLatest_ItMovesToEndOfSequence()
    {
        // Arrange
        var clock = clockFixture.Instance;

        // Use different TTG values to prove positioning is based on FeederFixEstimate, not LandingEstimate
        // flight1: FF=+5, TTG=10, Landing=+15
        // flight2: FF=+10, TTG=18, Landing=+28
        // flight3: FF=+15, TTG=12, Landing=+27
        // Initial order by FF: flight1, flight2, flight3
        // If ordered by Landing: flight1, flight3, flight2 (different!)
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(5))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(18)))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA789")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(15))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(12)))
            .WithRunway("34L")
            .Build();

        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg)
            .WithTrajectoryForFlight(flight1, new Trajectory(TimeSpan.FromMinutes(10)))
            .WithTrajectoryForFlight(flight2, new Trajectory(TimeSpan.FromMinutes(18)))
            .WithTrajectoryForFlight(flight3, new Trajectory(TimeSpan.FromMinutes(12)));

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        // Verify initial order (positioned by FeederFixEstimate)
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially (earliest FF)");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third initially (latest FF)");
        flight3.LandingEstimate.ShouldBeLessThan(flight2.LandingEstimate, "flight3 lands earlier than flight2, but positioned later due to FF");

        // Update QFA123 with a later FeederFixEstimate than all other flights
        var newFeederFixTime = clock.UtcNow().AddMinutes(20);

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
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(35))
            ]);

        var handler = GetHandler(instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should move to end of sequence (based on FF estimate)
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first after update (FF=+10)");
        sequence.NumberInSequence(flight3).ShouldBe(2, "QFA789 should be second after update (FF=+15)");
        sequence.NumberInSequence(flight1).ShouldBe(3, "QFA123 should be last after update (FF=+20, latest)");
        flight1.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight1.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    FlightUpdatedHandler GetHandler(
        IMaestroInstanceManager instanceManager,
        IClock clock,
        IArrivalLookup? arrivalLookup = null,
        ITrajectoryService? trajectoryService = null,
        IFlightUpdateRateLimiter? rateLimiter = null,
        IMaestroConnectionManager? connectionManager = null)
    {
        if (rateLimiter is null)
        {
            rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
            rateLimiter.ShouldUpdateFlight(Arg.Any<Flight>()).Returns(true);
        }

        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfigurationFixture.Instance]);

        arrivalLookup ??= Substitute.For<IArrivalLookup>();
        trajectoryService ??= new MockTrajectoryService();
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
