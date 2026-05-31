using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
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

public class CleanUpFlightsRequestHandlerTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();

    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;
    const int DefaultLostFlightTimeoutMinutes = 10;

    [Fact]
    public async Task WhenNoFlights_NothingIsRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenNoLandedFlights_NothingIsRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_now.AddMinutes(10))
            .WithState(State.Stable)
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(flight1, flight2))
            .Build();

        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1");
        session.FlightDataRecords["QFA2"] = MakeRecord("QFA2");

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.Count.ShouldBe(2);
        sequence.Flights.ShouldContain(flight1);
        sequence.Flights.ShouldContain(flight2);
    }

    [Fact]
    public async Task WhenFewLandedFlightsWithinTimeout_NothingIsRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 5)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.AddMinutes(-25))
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.Count.ShouldBe(5);
    }

    [Fact]
    public async Task WhenMoreThanMaxLandedFlights_ExcessFlightsAreRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 8)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.AddMinutes(-25))
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.Count.ShouldBe(5, "First 5 landed flights should remain");
        sequence.Flights.ShouldContain(landedFlights[0]);
        sequence.Flights.ShouldContain(landedFlights[1]);
        sequence.Flights.ShouldContain(landedFlights[2]);
        sequence.Flights.ShouldContain(landedFlights[3]);
        sequence.Flights.ShouldContain(landedFlights[4]);
        sequence.Flights.ShouldNotContain(landedFlights[5]);
        sequence.Flights.ShouldNotContain(landedFlights[6]);
        sequence.Flights.ShouldNotContain(landedFlights[7]);
    }

    [Fact]
    public async Task WhenLandedFlightExceedsTimeout_FlightIsRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var oldFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.AddMinutes(-35))
            .WithLandingTime(_now.AddMinutes(-15))
            .WithState(State.Landed)
            .Build();

        var recentFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_now.AddMinutes(-25))
            .WithLandingTime(_now.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(oldFlight, recentFlight))
            .Build();

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.Count.ShouldBe(1);
        sequence.Flights.ShouldNotContain(oldFlight);
        sequence.Flights.ShouldContain(recentFlight);
    }

    [Fact]
    public async Task WhenLandedFlightExactlyAtTimeout_FlightIsRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.AddMinutes(-30))
            .WithLandingTime(_now.AddMinutes(-10))
            .WithState(State.Landed)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenMixOfLandedAndNonLandedFlights_OnlyLandedFlightsAreAffected()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var unstableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var oldLandedFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_now.AddMinutes(-40))
            .WithLandingTime(_now.AddMinutes(-20))
            .WithState(State.Landed)
            .Build();

        var stableFlight = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_now.AddMinutes(10))
            .WithState(State.Stable)
            .Build();

        var recentLandedFlight = new FlightBuilder("QFA4")
            .WithFeederFixEstimate(_now.AddMinutes(-25))
            .WithLandingTime(_now.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(unstableFlight, oldLandedFlight, stableFlight, recentLandedFlight))
            .Build();

        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1");
        session.FlightDataRecords["QFA3"] = MakeRecord("QFA3");

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.Count.ShouldBe(3);
        sequence.Flights.ShouldContain(unstableFlight);
        sequence.Flights.ShouldContain(stableFlight);
        sequence.Flights.ShouldContain(recentLandedFlight);
        sequence.Flights.ShouldNotContain(oldLandedFlight);
    }

    [Fact]
    public async Task WhenMultipleFlightsExceedBothLimits_AllAreRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 10)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.AddMinutes(-40))
                .WithLandingTime(_now.AddMinutes(-20))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenAFlightIsLost_ItIsRemovedFromSequence()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithFeederFixEstimate(_now.AddMinutes(5))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1", lastSeen: _now.AddMinutes(-(DefaultLostFlightTimeoutMinutes + 1)));

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.ShouldBeEmpty("flight not seen within lost timeout should be removed");
    }

    [Fact]
    public async Task WhenAFlightHasNoFdrData_ItIsRemovedFromSequence()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithFeederFixEstimate(_now.AddMinutes(5))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        // No FlightDataRecord

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.ShouldBeEmpty("flight with no FDR data should be removed");
    }

    [Fact]
    public async Task WhenAManuallyInsertedFlightIsLost_ItIsNotRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("****01*")
            .AsManuallyInserted()
            .WithState(State.Frozen)
            .WithTargetLandingTime(_now.AddMinutes(10))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        // No FlightDataRecord for dummy flight

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.ShouldHaveSingleItem("manually inserted flights should never be removed due to lost timeout");
    }

    [Fact]
    public async Task WhenALostFlightHasLanded_ItIsNotRemovedByLostLogic()
    {
        // Landed flights are handled by landed cleanup, not lost-flight logic
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Landed)
            .WithFeederFixEstimate(_now.AddMinutes(-20))
            .WithLandingTime(_now.AddMinutes(-2))
            .Build();

        var (sessionManager, session, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1", lastSeen: _now.AddMinutes(-(DefaultLostFlightTimeoutMinutes + 1)));

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        // Flight is within landed retention window so it should remain
        sequence.Flights.ShouldHaveSingleItem("recently landed flight should not be removed by lost-flight logic");
    }

    [Fact]
    public async Task WhenADesequencedFlightIsLost_ItIsRemovedFromDesequencedList()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithFeederFixEstimate(_now.AddMinutes(5))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        session.DeSequencedFlights.Add(flight);
        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1", lastSeen: _now.AddMinutes(-(DefaultLostFlightTimeoutMinutes + 1)));

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        session.DeSequencedFlights.ShouldBeEmpty("lost desequenced flight should be removed");
    }

    [Fact]
    public async Task WhenAnUnactivatedFlightDataRecordIsStale_ItIsRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        // FDR for a flight that was never activated (not in sequence or desequenced list)
        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1", lastSeen: _now.AddMinutes(-(DefaultLostFlightTimeoutMinutes + 1)));

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        session.FlightDataRecords.ShouldNotContainKey("QFA1", "stale unactivated flight data record should be removed");
    }

    [Fact]
    public async Task WhenAnUnactivatedFlightDataRecordIsRecent_ItIsNotRemoved()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        session.FlightDataRecords["QFA1"] = MakeRecord("QFA1", lastSeen: _now.AddMinutes(-1));

        var handler = GetHandler(sessionManager, airportConfiguration);
        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        session.FlightDataRecords.ShouldContainKey("QFA1", "recent flight data record should not be removed");
    }

    [Fact]
    public async Task WhenNotMaster_DoesNothing()
    {
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 8)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.AddMinutes(-25))
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var configProvider = new AirportConfigurationProvider([airportConfiguration]);
        var handler = new CleanUpFlightsRequestHandler(
            new MockSlaveConnectionManager(),
            sessionManager,
            configProvider,
            clockFixture.Instance,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());

        await handler.Handle(new CleanUpFlightsRequest(airportConfiguration.Identifier), CancellationToken.None);

        sequence.Flights.Count.ShouldBe(8, "slave connections cannot modify the sequence locally");
    }

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways(DefaultRunway)
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = DefaultRunway,
                LandingRateSeconds = DefaultLandingRateSeconds,
                FeederFixes = []
            })
            .WithLostFlightTimeoutMinutes(DefaultLostFlightTimeoutMinutes)
            .Build();
    }

    FlightDataRecord MakeRecord(string callsign, DateTimeOffset? lastSeen = null)
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
            null,
            [],
            lastSeen ?? _now);
    }

    CleanUpFlightsRequestHandler GetHandler(ISessionManager sessionManager, AirportConfiguration airportConfiguration)
    {
        var configProvider = new AirportConfigurationProvider([airportConfiguration]);
        return new CleanUpFlightsRequestHandler(
            new MockLocalConnectionManager(),
            sessionManager,
            configProvider,
            clockFixture.Instance,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());
    }
}
