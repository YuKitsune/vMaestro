using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
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

public class ActivateFlightRequestHandlerTests(ClockFixture clockFixture)
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

    FlightDataRecord MakeRecord(string callsign, string origin = "YMML", FixEstimate[]? estimates = null)
    {
        var clock = clockFixture.Instance;
        return new FlightDataRecord(
            callsign,
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            origin,
            "YSSY",
            null,
            TimeSpan.FromHours(1),
            FlightPlanState.Active,
            _position,
            estimates ?? [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
            ],
            clock.UtcNow());
    }

    [Fact]
    public async Task WhenTheFlightIsAlreadyActive_ItIsNotDuplicated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var existingFlight = new FlightBuilder("QFA123")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(40))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(existingFlight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123");

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        sequence.Flights.Count(f => f.Callsign == "QFA123").ShouldBe(1);
    }

    [Fact]
    public async Task WhenFlightIsAlreadyDesequenced_ItIsNotDuplicated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var existingFlight = new FlightBuilder("QFA123")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(40))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();
        session.DeSequencedFlights.Add(existingFlight);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123");

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty("desequenced flight should not be re-inserted into sequence");
    }

    [Fact]
    public async Task WhenNoFdrExists_FlightIsNotActivated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenNoLandingEstimateExists_FlightIsNotActivated()
    {
        // Arrange - only a feeder fix estimate, no downstream estimate to use as landing
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: []);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenFeederFixExistsMultipleTimes_LastInstanceIsUsed()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

        var firstRivetEstimate = clock.UtcNow().AddMinutes(10);
        var lastRivetEstimate = clock.UtcNow().AddMinutes(15);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate("RIVET", firstRivetEstimate),
            new FixEstimate("RIVET", lastRivetEstimate),
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(35))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.FeederFixEstimate.ShouldBe(lastRivetEstimate);
    }

    [Fact]
    public async Task WhenOneRunwayIsActive_ThatRunwayIsAssigned()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithFeederFixes("RIVET")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123");

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.AssignedRunwayIdentifier.ShouldBe("34L");
    }

    [Theory]
    [InlineData("RIVET", "34L")]
    [InlineData("BOREE", "34R")]
    public async Task WhenMultipleRunwaysAreActive_RunwayIsAssignedBasedOnFeederFix(string feederFix, string expectedRunway)
    {
        // Arrange - default config: RIVET → 34L, BOREE → 34R
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate(feederFix, clock.UtcNow().AddMinutes(20)),
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.AssignedRunwayIdentifier.ShouldBe(expectedRunway);
    }

    [Fact]
    public async Task WhenMultipleRunwaysAreActive_AndNoRulesApply_FirstRunwayIsAssigned()
    {
        // Arrange - WELSH is a feeder fix but has no runway assignment rule; falls back to Default (34L)
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate("WELSH", clock.UtcNow().AddMinutes(20)),
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.AssignedRunwayIdentifier.ShouldBe("34L");
    }

    [Fact]
    public async Task WhenFlightIsActivated_EnrouteTrajectoryIsAssigned()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();

        var expectedEnrouteTrajectory = new EnrouteTrajectory(TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(2));
        var defaultTerminalTrajectory = new TerminalTrajectory(TimeSpan.FromMinutes(20));
        var trajectoryService = Substitute.For<ITrajectoryService>();
        trajectoryService
            .GetEnrouteTrajectory(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string>())
            .Returns(expectedEnrouteTrajectory);
        trajectoryService
            .GetTrajectory(
                Arg.Any<AircraftPerformanceData>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<Wind>())
            .Returns(defaultTerminalTrajectory);
        trajectoryService
            .GetTrajectory(
                Arg.Any<Flight>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<Wind>())
            .Returns(defaultTerminalTrajectory);

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(sb => sb.WithTrajectoryService(trajectoryService))
            .Build();
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123");

        var handler = GetHandler(sessionManager, airportConfiguration, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.EnrouteTrajectory.ShouldBe(expectedEnrouteTrajectory);
    }

    [Fact]
    public async Task WhenFlightIsActivated_TerminalTrajectoryIsAssigned()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();

        var expectedTerminalTrajectory = new TerminalTrajectory(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(35));
        var trajectoryService = Substitute.For<ITrajectoryService>();
        trajectoryService
            .GetEnrouteTrajectory(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string>())
            .Returns(new EnrouteTrajectory(TimeSpan.FromMinutes(8), TimeSpan.Zero));
        trajectoryService
            .GetTrajectory(
                Arg.Any<AircraftPerformanceData>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<Wind>())
            .Returns(expectedTerminalTrajectory);
        trajectoryService
            .GetTrajectory(
                Arg.Any<Flight>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<Wind>())
            .Returns(expectedTerminalTrajectory);

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(sb => sb.WithTrajectoryService(trajectoryService))
            .Build();
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123");

        var handler = GetHandler(sessionManager, airportConfiguration, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.TerminalTrajectory.ShouldBe(expectedTerminalTrajectory);
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    public async Task WhenEstimateIsAheadOfUnstableOrStableFlight_NewFlightOvertakes(State existingState)
    {
        // Arrange
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();
        var existingFlight = new FlightBuilder("QFA456")
            .WithState(existingState)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(30))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(50))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(existingFlight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20)),
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var newFlight = sequence.FindFlight("QFA123");
        newFlight.ShouldNotBeNull();
        sequence.NumberInSequence(newFlight).ShouldBe(1, "QFA123 should be first (earlier estimate)");
        sequence.NumberInSequence(existingFlight).ShouldBe(2);
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenEstimateIsAheadOfSuperStableOrFrozenFlight_NewFlightIsInsertedBehind(State existingState)
    {
        // Arrange
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();
        var existingFlight = new FlightBuilder("QFA456")
            .WithState(existingState)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(30))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(50))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(existingFlight))
            .Build();

        // QFA123 has an earlier estimate than QFA456 but cannot overtake it
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20)),
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var newFlight = sequence.FindFlight("QFA123");
        newFlight.ShouldNotBeNull();
        sequence.NumberInSequence(existingFlight).ShouldBe(1);
        sequence.NumberInSequence(newFlight).ShouldBe(2, "QFA123 cannot overtake a SuperStable/Frozen flight");
    }

    [Fact]
    public async Task WhenEstimateIsAheadOfLandedFlight_NewFlightIsInsertedBehind()
    {
        // Arrange - Landed flights are excluded from NumberInSequence, so assert raw insertion order
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();
        var existingFlight = new FlightBuilder("QFA456")
            .WithState(State.Landed)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(30))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(50))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(existingFlight))
            .Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20)),
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var existingIndex = sequence.Flights.ToList().FindIndex(f => f.Callsign == "QFA456");
        var newIndex = sequence.Flights.ToList().FindIndex(f => f.Callsign == "QFA123");
        newIndex.ShouldNotBe(-1);
        newIndex.ShouldBeGreaterThan(existingIndex, "QFA123 cannot overtake a Landed flight");
    }

    [Fact]
    public async Task WhenNoFeederFixCouldBeFound_HighPriorityIsAssigned()
    {
        // Arrange - no RIVET/BOREE/WELSH estimate, only YSSY; feeder fix will be null
        var clock = clockFixture.Instance;
        var airportConfiguration = GetDefaultAirportConfiguration();
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration).Build();

        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123", estimates: [
            new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
        ]);

        var handler = GetHandler(sessionManager, airportConfiguration);

        // Act
        await handler.Handle(new ActivateFlightRequest("YSSY", "QFA123"), CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.HighPriority.ShouldBeTrue();
    }

    ActivateFlightRequestHandler GetHandler(
        ISessionManager sessionManager,
        AirportConfiguration airportConfiguration,
        ITrajectoryService? trajectoryService = null,
        IMediator? mediator = null)
    {
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        trajectoryService ??= new MockTrajectoryService();
        mediator ??= Substitute.For<IMediator>();

        return new ActivateFlightRequestHandler(
            sessionManager,
            airportConfigurationProvider,
            trajectoryService,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());
    }
}
