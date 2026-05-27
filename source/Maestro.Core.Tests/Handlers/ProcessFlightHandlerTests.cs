using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Sessions.Contracts;
using Maestro.Core.Sessions.Handlers;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ProcessFlightHandlerTests(ClockFixture clockFixture)
{
    readonly FlightPosition _position = new(
        new Coordinate(0, 0),
        0,
        VerticalTrack.Maintaining,
        0,
        false);

    static AirportConfiguration GetDefaultAirportConfiguration(int lostFlightTimeoutMinutes = 10)
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithFeederFixes("RIVET", "BOREE", "WELSH")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithDepartureAirport("YSCB", [new AllAircraftTypesDescriptor()], 15)
            .WithLostFlightTimeoutMinutes(lostFlightTimeoutMinutes)
            .Build();
    }

    FlightDataRecord MakeRecord(string callsign, FlightPosition? position = null, FixEstimate[]? estimates = null, DateTimeOffset? lastSeen = null)
    {
        return new FlightDataRecord(
            callsign,
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            null,
            position ?? _position,
            estimates ?? [],
            lastSeen ?? clockFixture.Instance.UtcNow());
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAnExistingFlightHasFdrData_ItsEstimatesAreRecalculated(State state)
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

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", newFeederFixTime), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(26))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldNotBe(clock.UtcNow().AddMinutes(26));
        flight.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightHasFdrData_AndItIsNotTrackingViaAFeederFix_EstimatesAreRecalculated()
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

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var newLandingEstimate = clock.UtcNow().AddMinutes(25);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("YSSY", newLandingEstimate)]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newLandingEstimate.Subtract(ttg));
        flight.LandingEstimate.ShouldBe(newLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightHasFdrData_AndItPassedTheFeederFix_EstimatesAreUpdated()
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
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var pastFeederFixEstimate = clock.UtcNow().AddMinutes(-2);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", pastFeederFixEstimate), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(pastFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(pastFeederFixEstimate.Add(ttg));
    }

    [Fact]
    public async Task WhenAnExistingFlightHasFdrData_AndItHasPassedTheFeederFix_EstimatesAreNoLongerUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(10);
        var trajectoryService = new MockTrajectoryService(ttg);

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(-5))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(5))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var originalFeederFixEstimate = flight.FeederFixEstimate;
        var originalLandingEstimate = flight.LandingEstimate;

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10)), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(20))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(originalLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightHasFdrData_ButNoPositionIsAvailable_EstimatesAreNotRecalculated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var originalFeederFixTime = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixTime)
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA123"] = new FlightDataRecord(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", null, null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(1))],
            clock.UtcNow());

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixTime);
    }

    [Fact]
    public async Task WhenAnExistingFlightHasFdrData_AndTheFeederFixEstimateWasManuallyAssigned_EstimatesAreNotUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var manualFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(manualFeederFixEstimate, manual: true)
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15)), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(manualFeederFixEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightHasFdrData_AllFlightDataIsUpdated()
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

        var newPosition = new FlightPosition(new Coordinate(1, 1), 38_000, VerticalTrack.Descending, 280, false);
        session.FlightDataRecords["QFA123"] = new FlightDataRecord(
            "QFA123", "B744", AircraftCategory.Jet, WakeCategory.Heavy,
            "YMAV", "YSSY", clock.UtcNow().AddHours(-1.5),
            newPosition,
            [new FixEstimate("WELSH", clock.UtcNow().AddMinutes(10))],
            clock.UtcNow());

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.AircraftType.ShouldBe("B744");
        flight.WakeCategory.ShouldBe(WakeCategory.Heavy);
        flight.OriginIdentifier.ShouldBe("YMAV");
        flight.Position!.Coordinate.Latitude.ShouldBe(newPosition.Coordinate.Latitude);
        flight.Position.Altitude.ShouldBe(newPosition.Altitude);
        flight.LastSeen.ShouldBe(clock.UtcNow());
    }

    [Fact]
    public async Task WhenADesequencedFlightHasFdrData_ItsEstimatesAreStillUpdated()
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
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", newFeederFixTime), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(26))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    [Fact]
    public async Task WhenAnUnstableFlightHasFdrData_ItsPositionInSequenceIsRecalculated()
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
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight2, flight1))
            .Build();

        sequence.NumberInSequence(flight2).ShouldBe(1);
        sequence.NumberInSequence(flight1).ShouldBe(2);

        var newFeederFixTime = clock.UtcNow().AddMinutes(2);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", newFeederFixTime), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(12))]);
        session.FlightDataRecords["QFA456"] = MakeRecord("QFA456",
            estimates: [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(5))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first after update (earlier landing estimate)");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second (later landing estimate)");
        flight1.LandingEstimate.ShouldBe(newFeederFixTime.Add(ttg));
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAStableFlightHasFdrData_ItsPositionInSequenceIsNotRecalculated(State state)
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;

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

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight1, flight2))
            .Build();

        sequence.NumberInSequence(flight1).ShouldBe(1);
        sequence.NumberInSequence(flight2).ShouldBe(2);

        var newFeederFixTime = clock.UtcNow().AddMinutes(5);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10))]);
        session.FlightDataRecords["QFA456"] = MakeRecord("QFA456",
            estimates: [new FixEstimate("RIVET", newFeederFixTime), new FixEstimate("YSSY", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should remain first");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should remain second");

        flight2.FeederFixEstimate.ShouldBe(newFeederFixTime, "estimates should be updated even for stable flights");
    }

    [Fact]
    public async Task WhenAFlightHasNoFdrDataAndIsNotManuallyInserted_ItIsRemovedFromSequence()
    {
        // Arrange - #98: disconnected aircraft should be removed
        var airportConfiguration = GetDefaultAirportConfiguration(lostFlightTimeoutMinutes: 10);
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Frozen)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(5))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // No FlightDataRecord for this flight

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty("flight with no FDR data should be removed");
    }

    [Fact]
    public async Task WhenAFlightFdrDataIsStale_ItIsRemovedFromSequence()
    {
        // Arrange - #98: disconnected aircraft should be removed after timeout
        var airportConfiguration = GetDefaultAirportConfiguration(lostFlightTimeoutMinutes: 10);
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Frozen)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(5))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            lastSeen: clock.UtcNow().AddMinutes(-11));

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty("flight not seen within lost timeout should be removed");
    }

    [Fact]
    public async Task WhenAManuallyInsertedFlightHasNoFdrData_ItIsNotRemoved()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration(lostFlightTimeoutMinutes: 10);
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("****01*")
            .AsManuallyInserted()
            .WithState(State.Frozen)
            .WithTargetLandingTime(clock.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // No FlightDataRecord for dummy flight

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldHaveSingleItem("manually inserted flights should never be removed by ProcessFlightsHandler");
    }

    [Fact]
    public async Task WhenAFlightFdrDataIsStale_ButFlightHasLanded_ItIsNotRemovedByProcessFlights()
    {
        // Arrange - landed flights are handled by CleanUpLandedFlightsRequestHandler
        var airportConfiguration = GetDefaultAirportConfiguration(lostFlightTimeoutMinutes: 10);
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Landed)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(-20))
            .WithLandingTime(clock.UtcNow().AddMinutes(-2))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            lastSeen: clock.UtcNow().AddMinutes(-15));

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldHaveSingleItem("landed flights should be left for CleanUpLandedFlightsRequestHandler");
    }

    [Fact]
    public async Task WhenAFlightHasNoFdrData_StateIsStillUpdatedBasedOnTime()
    {
        // Arrange - #84: state transitions should happen on timer, not FDR arrival
        var airportConfiguration = GetDefaultAirportConfiguration(lostFlightTimeoutMinutes: 5);
        var clock = clockFixture.Instance;

        // Flight is Frozen but LandingTime is now in the past - should transition to Landed
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Frozen)
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(-30))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(-2))
            .WithLandingTime(clock.UtcNow().AddMinutes(-2))
            .WithActivationTime(clock.UtcNow().AddHours(-2))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // Stale FDR data (over 5 minute timeout) but flight is about to land
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            lastSeen: clock.UtcNow().AddMinutes(-6));

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        // Flight transitions to Landed via UpdateStateBasedOnTime before the lost check removes it
        sequence.Flights.ShouldHaveSingleItem("flight should transition to Landed, not be removed as lost");
        sequence.Flights[0].State.ShouldBe(State.Landed);
    }

    [Fact]
    public async Task WhenInSlaveMode_NothingIsProcessed()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            lastSeen: clock.UtcNow().AddMinutes(-15));

        var handler = GetHandler(airportConfiguration, sessionManager, clock,
            connectionManager: new MockSlaveConnectionManager());

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldHaveSingleItem("slave should not process or remove flights");
    }

    ProcessFlightsHandler GetHandler(
        AirportConfiguration airportConfiguration,
        ISessionManager sessionManager,
        IClock clock,
        ITrajectoryService? trajectoryService = null,
        IMaestroConnectionManager? connectionManager = null)
    {
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        trajectoryService ??= new MockTrajectoryService();
        connectionManager ??= new MockLocalConnectionManager();
        var mediator = Substitute.For<IMediator>();

        return new ProcessFlightsHandler(
            sessionManager,
            connectionManager,
            airportConfigurationProvider,
            trajectoryService,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
