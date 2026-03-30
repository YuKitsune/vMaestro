using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Handlers;
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

public class FlightUpdatedHandlerTests(ClockFixture clockFixture)
{
    readonly FlightPosition _position = new(
        new Coordinate(0, 0),
        0,
        VerticalTrack.Maintaining,
        0,
        false);

    static AirportConfiguration GetDefaultAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithFeederFixes("RIVET", "BOREE", "WELSH")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithDepartureAirport("YSCB", [new AllAircraftTypesDescriptor()], 15)
            .Build();
    }

    [Fact]
    public async Task WhenAFlightIsOutOfRangeOfFeederFix_ItShouldNotBeTracked()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenAFlightIsInRangeOfFeederFix_ItShouldBeTracked()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var session = await sessionManager.GetSession(sequence.AirportIdentifier, CancellationToken.None);
        var flight = session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe(notification.Callsign);
    }

    [Fact]
    public async Task WhenAFlightIsUncoupledAtDepartureAirport_ItShouldBeAddedToThePendingList()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var session = await sessionManager.GetSession(sequence.AirportIdentifier, CancellationToken.None);
        var flight = session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe(notification.Callsign);
    }

    [Fact]
    public async Task WhenAFlightIsOnGroundAtNonDepartureAirport_ItShouldNotBeTracked()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        // Create a flight not tracking via any feeder fix
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix(null)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newLandingEstimate.Subtract(ttg));
        flight.LandingEstimate.ShouldBe(newLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItPassesTheFeederFix_EstimatesAreUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        // New estimate has the feeder fix in the past (flight just crossed it)
        var pastFeederFixEstimate = clock.UtcNow().AddMinutes(-2);

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
                new FixEstimate("RIVET", pastFeederFixEstimate),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(30))
            ]);

        var handler = GetHandler(airportConfiguration, instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(pastFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(pastFeederFixEstimate.Add(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItIsNotTrackingViaAFeederFix_AndItPassesTheVirtualFeederFixPoint_EstimatesAreUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix(null)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithFlight(flight))
            .Build();

        // Landing estimate that puts the virtual feeder fix point at exactly now
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

        var handler = GetHandler(airportConfiguration, instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.LandingEstimate.ShouldBe(newLandingEstimate);
        flight.FeederFixEstimate.ShouldBe(newLandingEstimate.Subtract(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndItHasPassedTheFeederFix_EstimatesAreNoLongerUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        // Flight with a feeder fix estimate already in the past (has crossed the FF)
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(-5))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(5))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var originalFeederFixEstimate = flight.FeederFixEstimate;
        var originalLandingEstimate = flight.LandingEstimate;

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
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(20))
            ]);

        var handler = GetHandler(airportConfiguration, instanceManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(originalLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_ButNoPositionIsAvailable_EstimatesAreNotRecalculated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var originalFeederFixTime = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixTime)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixTime);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndTheFeederFixEstimateWasManuallyAssigned_EstimatesAreNotUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var manualFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(manualFeederFixEstimate, manual: true)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(manualFeederFixEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AllFlightDataIsUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

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
        session.FlightDataRecords["QFA123"].Estimates.ShouldHaveSingleItem().ShouldBe(notification.Estimates.Single());
        flight.LastSeen.ShouldBe(clock.UtcNow());
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_ItsEstimatesAreStillUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var ttg = TimeSpan.FromMinutes(10);
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();
        session.DeSequencedFlights.Add(flight);

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(15))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight2, flight1))
            .Build();

        sequence.NumberInSequence(flight2).ShouldBe(1);
        sequence.NumberInSequence(flight1).ShouldBe(2);

        // Update QFA123 with an earlier LandingEstimate
        var newFeederFixTime = clock.UtcNow().AddMinutes(2);
        var newLandingEstimate = clock.UtcNow().AddMinutes(12);

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
                new FixEstimate("YSSY", newLandingEstimate)
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first after update (earlier landing estimate)");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second (later landing estimate)");
        flight1.LandingEstimate.ShouldBe(newLandingEstimate);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAStableFlightIsUpdated_ItsPositionInSequenceIsNotRecalculated(State state)
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;

        // Sequences are ordered by landing time
        // flight1: FF=+10, TTG=10, Landing=+20 (Frozen, immovable)
        // flight2: FF=+20, TTG=10, Landing=+30 (state, Stable/SuperStable/Frozen)
        // Initial order by landing time: flight1 first (+20), flight2 second (+30)
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Frozen)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new TerminalTrajectory(TimeSpan.FromMinutes(10), default, default))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(TimeSpan.FromMinutes(10), default, default))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService()
            .WithTrajectoryForFlight(flight1, new TerminalTrajectory(TimeSpan.FromMinutes(10), default, default))
            .WithTrajectoryForFlight(flight2, new TerminalTrajectory(TimeSpan.FromMinutes(10), default, default));

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sessionManager.SessionExists("YBBN").ShouldBeFalse("no instance should exist for YBBN");
    }

    [Fact]
    public async Task WhenFlightIsNotTrackingViaKnownFeederFix_ItIsAddedToThePendingList()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe("QFA123");
        flight.IsHighPriority.ShouldBeTrue("flights without feeder fix should be high priority");
    }

    [Fact]
    public async Task WhenFlightIsNotCurrentlyTracked_RunwayIsAssignedBasedOnFeederFixPreferences()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var stableFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(25))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(stableFlight))
            .Build();

        // New flight with earlier LandingEstimate
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
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        sequence.NumberInSequence(newFlight).ShouldBe(1, "new flight should be inserted before stable flight (earlier landing estimate)");
        sequence.NumberInSequence(stableFlight).ShouldBe(2, "stable flight should now be second (later landing estimate)");
    }

    [Fact]
    public async Task WhenNewFlightLandingEstimateIsEarlierThanSuperStableFlight_FlightIsInsertedAfter()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var superStableFlight = new FlightBuilder("QFA456")
            .WithState(State.SuperStable)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(superStableFlight))
            .Build();

        // New flight with earlier LandingEstimate
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

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        sequence.NumberInSequence(superStableFlight).ShouldBe(1, "superstable flight should remain first (cannot be overtaken)");
        sequence.NumberInSequence(newFlight).ShouldBe(2, "new flight inserted after superstable flight despite earlier landing estimate");
    }

    [Fact]
    public async Task WhenUnstableLandingEstimateIsAheadOfStableFlight_ItDoesNotOvertakeStableFlight()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var stableFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var unstableFlight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(43))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(stableFlight, unstableFlight))
            .Build();

        sequence.NumberInSequence(stableFlight).ShouldBe(1);
        sequence.NumberInSequence(unstableFlight).ShouldBe(2);

        // Update unstable flight with earlier LandingEstimate (would move it ahead if both were unstable)
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
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(20))
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Unstable should NOT overtake stable flight
        sequence.NumberInSequence(stableFlight).ShouldBe(1, "stable flight should remain first (unstable cannot overtake)");
        sequence.NumberInSequence(unstableFlight).ShouldBe(2, "unstable flight should remain second (cannot overtake stable)");
    }

    [Fact]
    public async Task WhenLastUpdateWasRecent_TheUpdateIsIgnored()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var originalEstimate = clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalEstimate)
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // Pre-populate the flight data store so the rate limiter has a LastSeen to check
        session.FlightDataRecords["QFA123"] = new FlightDataRecord(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", null, _position,
            [new FixEstimate("RIVET", originalEstimate)],
            clock.UtcNow());

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
        rateLimiter.ShouldUpdate(Arg.Any<DateTimeOffset>()).Returns(false); // Rate limit triggered

        var handler = GetHandler(airportConfiguration, sessionManager, clock, rateLimiter: rateLimiter);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

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
        var handler = GetHandler(airportConfiguration, sessionManager, clock, connectionManager: slaveConnectionManager);

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
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var pendingFlight = new PendingFlight("QFA123", IsFromDepartureAirport: false, IsHighPriority: false);

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();
        session.PendingFlights.Add(pendingFlight);

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Flight should remain in pending list only
        session.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should remain in pending list");
        sequence.Flights.ShouldBeEmpty("pending flight should NOT be added to the sequence");
        session.FlightDataRecords["QFA123"].LastSeen.ShouldBe(clock.UtcNow(), "flight data store should be updated");
    }

    [Fact]
    public async Task WhenAPendingFlightIsUpdated_ItRemainsInThePendingList()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var pendingFlight = new PendingFlight("QFA123", IsFromDepartureAirport: false, IsHighPriority: false);

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();
        session.PendingFlights.Add(pendingFlight);

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - pending flight stays in list and is not promoted to sequence
        session.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should remain in pending list");
        sequence.Flights.ShouldBeEmpty("pending flight should not be in the sequence");
        // Flight data store is updated with the latest vatSys data
        session.FlightDataRecords["QFA123"].Estimates.ShouldContain(e => e.FixIdentifier == "RIVET", "flight data store should have new estimates");
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_ItShouldNotBeAddedToTheSequence()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();
        session.DeSequencedFlights.Add(flight);

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

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Flight should remain in desequenced list and estimates should be updated
        session.DeSequencedFlights.ShouldContain(flight, "flight should remain in desequenced list");
        sequence.Flights.ShouldBeEmpty("desequenced flight should NOT be added to the sequence");
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime, "desequenced flight estimates should be updated");
        flight.LandingEstimate.ShouldBe(newLandingTime, "desequenced flight estimates should be updated");
    }

    [Fact]
    public async Task WhenUnstableEstimateMovesBack_PositionIsCalculatedCorrectly()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(15))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA789")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(25))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        // Update QFA123 with a later LandingEstimate, moving it between QFA456 and QFA789
        var newFeederFixEstimate = clock.UtcNow().AddMinutes(12);

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
                new FixEstimate("RIVET", newFeederFixEstimate),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(22))
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first after update");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second after moving back");
        sequence.NumberInSequence(flight3).ShouldBe(3, "QFA789 should be third after update");
        flight1.LandingEstimate.ShouldBe(newFeederFixEstimate.Add(ttg));
    }

    [Fact]
    public async Task WhenUnstableEstimateBecomesLatest_ItMovesToEndOfSequence()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA789")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithLandingEstimate(clock.UtcNow().AddMinutes(25))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);
        sequence.NumberInSequence(flight3).ShouldBe(3);

        // Update QFA123 with a later LandingEstimate than all other flights
        var newLandingEstimate = clock.UtcNow().AddMinutes(30);

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
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20)),
                new FixEstimate("YSSY", newLandingEstimate)
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first after update");
        sequence.NumberInSequence(flight3).ShouldBe(2, "QFA789 should be second after update");
        sequence.NumberInSequence(flight1).ShouldBe(3, "QFA123 should be last after update");
        flight1.LandingEstimate.ShouldBe(newLandingEstimate);
    }

    [Fact]
    public async Task WhenAFlightIsUpdated_TheFlightDataRecordIsStored()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        var feederFixEstimate = clock.UtcNow().AddMinutes(30);
        var landingEstimate = clock.UtcNow().AddMinutes(50);

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
                new FixEstimate("RIVET", feederFixEstimate),
                new FixEstimate("YSSY", landingEstimate)
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.FlightDataRecords.TryGetValue("QFA123", out var record).ShouldBeTrue("FlightDataRecord should be stored on update");
        record!.Callsign.ShouldBe("QFA123");
        record.AircraftType.ShouldBe("B738");
        record.WakeCategory.ShouldBe(WakeCategory.Medium);
        record.Origin.ShouldBe("YMML");
        record.Destination.ShouldBe("YSSY");
        record.LastSeen.ShouldBe(clock.UtcNow());
        record.Estimates.ShouldContain(e => e.FixIdentifier == "RIVET" && e.Estimate == feederFixEstimate);
        record.Estimates.ShouldContain(e => e.FixIdentifier == "YSSY" && e.Estimate == landingEstimate);
    }

    [Fact]
    public async Task WhenAFlightIsUpdated_AndNoEstimatesAreAvailable_ItIsAddedToThePendingList()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

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
            []); // No estimates at all

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.PendingFlights.ShouldHaveSingleItem().Callsign.ShouldBe("QFA123");
        sequence.Flights.ShouldBeEmpty("flight with no estimates should not be in the sequence");
    }

    [Fact]
    public async Task WhenAFlightIsUpdated_AndFeederFixEstimateIsNull_ItIsAddedToThePendingList()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

        // Feeder fix is present in the route but has no estimate (vatSys uses null when the estimate is not yet known)
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
                new FixEstimate("RIVET", null), // Feeder fix with no estimate
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(50))
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var pendingFlight = session.PendingFlights.ShouldHaveSingleItem();
        pendingFlight.Callsign.ShouldBe("QFA123");
        pendingFlight.IsHighPriority.ShouldBeFalse("flight has a feeder fix, estimate is just not yet known");
        sequence.Flights.ShouldBeEmpty("flight with no feeder fix estimate should not be in the sequence");
    }

    FlightUpdatedHandler GetHandler(
        AirportConfiguration airportConfiguration,
        ISessionManager sessionManager,
        IClock clock,
        ITrajectoryService? trajectoryService = null,
        IFlightUpdateRateLimiter? rateLimiter = null,
        IMaestroConnectionManager? connectionManager = null)
    {
        if (rateLimiter is null)
        {
            rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
            rateLimiter.ShouldUpdate(Arg.Any<DateTimeOffset>()).Returns(true);
        }

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);

        trajectoryService ??= new MockTrajectoryService();
        connectionManager ??= new MockLocalConnectionManager();
        var mediator = Substitute.For<IMediator>();

        return new FlightUpdatedHandler(
            sessionManager,
            connectionManager,
            rateLimiter,
            airportConfigurationProvider,
            trajectoryService,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
