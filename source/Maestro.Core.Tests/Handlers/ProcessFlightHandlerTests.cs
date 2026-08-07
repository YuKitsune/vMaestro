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
            TimeSpan.FromHours(1),
            FlightPlanState.Active,
            position ?? _position,
            estimates ?? [],
            lastSeen ?? clockFixture.Instance.UtcNow());
    }

    [Fact]
    public async Task WhenNoFdrExistsForAFlight_NothingChanges()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var originalFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixEstimate)
            .WithAircraftType("B738")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // No FDR added to session.FlightDataRecords

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.AircraftType.ShouldBe("B738");
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate);
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
            "YMML", "YSSY", null, TimeSpan.FromHours(1), FlightPlanState.Active, null,
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
    public async Task WhenProcessingAnUnstableFlight_AndDelayIsBeingAbsorbed_RequiredDelayIsRecalculated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(20);

        // QFA456 (Frozen) lands at +30min; 3min runway separation means QFA123 needs STA = +33min
        var frozenFlight = new FlightBuilder("QFA456")
            .WithState(State.Frozen)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithLandingTime(clock.UtcNow().AddMinutes(30))
            .Build();

        // QFA123 (Unstable): ETA_FF = +10, ETA = +30, STA_FF = +13, STA = +33 → RequiredEnrouteDelay = 3min
        var etaFF = clock.UtcNow().AddMinutes(10);
        var staFF = etaFF.AddMinutes(3);
        var eta = etaFF.Add(ttg);
        var sta = eta.AddMinutes(3);

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(etaFF)
            .WithFeederFixTime(staFF)
            .WithLandingEstimate(eta)
            .WithLandingTime(sta)
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(frozenFlight, flight))
            .Build();

        // FDR moves ETA_FF by +2min: new ETA = +32, which still needs 1min of separation from QFA456
        var updatedEtaFF = etaFF.AddMinutes(2);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", updatedEtaFF), new FixEstimate("YSSY", updatedEtaFF.Add(ttg))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert - for Unstable flights the scheduler recalculates Required, so Required = Remaining
        flight.RequiredEnrouteDelay.ShouldBe(TimeSpan.FromMinutes(1), "required delay is recalculated after rescheduling");
        flight.RemainingEnrouteDelay.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenProcessingAStableFlight_AndDelayIsBeingAbsorbed_RequiredDelayIsUnchanged(State state)
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(20);

        // Flight requires 3min of enroute delay: STA_FF = ETA_FF + 3min
        // ETA = ETA_FF + TTG, STA = ETA + 3min (same total delay)
        var etaFF = clock.UtcNow().AddMinutes(30);
        var eta = etaFF.Add(ttg);
        var staFF = etaFF.AddMinutes(3);
        var sta = eta.AddMinutes(3);

        // Frozen blocker occupies the landing slot at eta, forcing QFA123 to be scheduled at eta+3min
        var blocker = new FlightBuilder("QFA456")
            .WithState(State.Frozen)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(etaFF)
            .WithLandingEstimate(eta)
            .WithLandingTime(eta)
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(etaFF)
            .WithFeederFixTime(staFF)
            .WithLandingEstimate(eta)
            .WithLandingTime(sta)
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(blocker, flight))
            .Build();

        // FDR updates ETA_FF by +2min: flight has absorbed 2min of its 3min delay
        flight.SetState(state, clockFixture.Instance);
        var updatedEtaFF = etaFF.AddMinutes(2);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [new FixEstimate("RIVET", updatedEtaFF), new FixEstimate("YSSY", updatedEtaFF.Add(ttg))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert - for non-Unstable flights the required delay is not recalculated
        flight.RequiredEnrouteDelay.ShouldBe(TimeSpan.FromMinutes(3), "required delay should not change");
        flight.RemainingEnrouteDelay.ShouldBe(TimeSpan.FromMinutes(1), "remaining delay should reflect absorbed delay");
    }

    [Fact]
    public async Task WhenProcessingAnUnstableFlight_AndTheFdrShowsADifferentFeederFix_TheFeederFixAndTrajectoriesAreUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;

        // Flight starts via RIVET, FDR will report BOREE
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(40))
            .Build();

        var expectedTerminalTrajectory = new TerminalTrajectory(TimeSpan.FromMinutes(25));
        var expectedEnrouteTrajectory = new EnrouteTrajectory(TimeSpan.FromMinutes(5), TimeSpan.Zero);

        var trajectoryService = Substitute.For<ITrajectoryService>();
        trajectoryService
            .GetTrajectory(
                Arg.Any<Flight>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<Wind>())
            .Returns(expectedTerminalTrajectory);
        trajectoryService
            .GetEnrouteTrajectory(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string>())
            .Returns(expectedEnrouteTrajectory);

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        // FDR now reports BOREE as the feeder fix
        var boreeEstimate = clock.UtcNow().AddMinutes(15);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [
                new FixEstimate("BOREE", boreeEstimate),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(40))
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert
        flight.FeederFixIdentifier.ShouldBe("BOREE");
        flight.TerminalTrajectory.ShouldBe(expectedTerminalTrajectory);
        flight.EnrouteTrajectory.ShouldBe(expectedEnrouteTrajectory);
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
            TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
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
    public async Task WhenProcessingAnUnstableFlight_AndItsEstimateMovesAheadOfAStableFlight_ItDoesNotOvertake(State blockerState)
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var ttg = TimeSpan.FromMinutes(20);

        // Blocker flight is Stable/SuperStable/Frozen at position 1
        var blocker = new FlightBuilder("QFA456")
            .WithState(blockerState)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(30))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(50))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        // Unstable flight at position 2 with a later estimate
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(40))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(60))
            .WithTrajectory(new TerminalTrajectory(ttg))
            .Build();

        var trajectoryService = new MockTrajectoryService(ttg);
        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(blocker, flight))
            .Build();

        sequence.NumberInSequence(blocker).ShouldBe(1);
        sequence.NumberInSequence(flight).ShouldBe(2);

        // FDR updates QFA123's estimate to be earlier than the blocker
        var updatedEtaFF = clock.UtcNow().AddMinutes(10);
        session.FlightDataRecords["QFA123"] = MakeRecord("QFA123",
            estimates: [
                new FixEstimate("RIVET", updatedEtaFF),
                new FixEstimate("YSSY", updatedEtaFF.Add(ttg))
            ]);
        session.FlightDataRecords["QFA456"] = MakeRecord("QFA456",
            estimates: [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, trajectoryService: trajectoryService);

        // Act
        await handler.Handle(new ProcessFlightsRequest("YSSY"), CancellationToken.None);

        // Assert - Unstable flight cannot overtake the Stable/SuperStable/Frozen blocker
        sequence.NumberInSequence(blocker).ShouldBe(1, "blocker should remain first");
        sequence.NumberInSequence(flight).ShouldBe(2, "unstable flight cannot overtake a stable/frozen flight");
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
