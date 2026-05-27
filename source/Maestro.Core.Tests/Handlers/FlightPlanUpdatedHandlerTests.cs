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

public class FlightPlanUpdatedHandlerTests(ClockFixture clockFixture)
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
    public async Task WhenAFlightIsUpdated_TheFlightDataRecordIsStored()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        var feederFixEstimate = clock.UtcNow().AddMinutes(30);
        var landingEstimate = clock.UtcNow().AddMinutes(50);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            _position,
            [
                new FixEstimate("RIVET", feederFixEstimate),
                new FixEstimate("YSSY", landingEstimate)
            ]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.FlightDataRecords.TryGetValue("QFA123", out var record).ShouldBeTrue();
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
    public async Task WhenAFlightIsUpdated_AndItIsAlreadyKnown_FlightDataRecordIsUpdated()
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

        var newFeederFixEstimate = clock.UtcNow().AddMinutes(15);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B744", AircraftCategory.Jet, WakeCategory.Heavy,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            _position,
            [new FixEstimate("RIVET", newFeederFixEstimate)]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.FlightDataRecords.TryGetValue("QFA123", out var record).ShouldBeTrue();
        record!.AircraftType.ShouldBe("B744");
        record.WakeCategory.ShouldBe(WakeCategory.Heavy);
    }

    [Fact]
    public async Task WhenAFlightIsUpdated_AndItIsAlreadyKnown_InsertFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<InsertFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenANewFlightIsUpdated_InsertFlightRequestIsSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<InsertFlightRequest>(r =>
                r.Callsign == "QFA123" &&
                r.AirportIdentifier == "YSSY" &&
                r.Options is FdrInsertionOptions),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAPendingFlightIsUpdated_InsertFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();
        session.PendingFlights.Add(new PendingFlight("QFA123", IsFromDepartureAirport: false, IsHighPriority: false));

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<InsertFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_InsertFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();
        session.DeSequencedFlights.Add(flight);

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<InsertFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenLastUpdateWasRecent_TheUpdateIsIgnored()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .Build();

        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        session.FlightDataRecords["QFA123"] = new FlightDataRecord(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", null, _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20))],
            clock.UtcNow());

        var originalLastSeen = session.FlightDataRecords["QFA123"].LastSeen;

        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdate(Arg.Any<DateTimeOffset>()).Returns(false);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, rateLimiter: rateLimiter);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.FlightDataRecords["QFA123"].LastSeen.ShouldBe(originalLastSeen, "FlightDataRecord should not update when rate-limited");
    }

    [Fact]
    public async Task WhenInSlaveMode_UpdateIsRelayedToMaster()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var handler = GetHandler(airportConfiguration, sessionManager, clock, connectionManager: slaveConnectionManager);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedNotifications.Count.ShouldBe(1, "notification should be relayed to master");
        slaveConnectionManager.Connection.InvokedNotifications[0].ShouldBe(notification);
    }

    [Fact]
    public async Task WhenInSlaveMode_FlightDataRecordIsNotUpdatedLocally()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var handler = GetHandler(airportConfiguration, sessionManager, clock, connectionManager: slaveConnectionManager);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.FlightDataRecords.ShouldNotContainKey("QFA123", "slave should not update FlightDataRecords locally");
    }

    [Fact]
    public async Task WhenNoSessionExistsForDestination_NothingHappens()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YBBN", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(airportConfiguration, sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<InsertFlightRequest>(), Arg.Any<CancellationToken>());
    }

    FlightPlanUpdatedHandler GetHandler(
        AirportConfiguration airportConfiguration,
        ISessionManager sessionManager,
        IClock clock,
        IFlightUpdateRateLimiter? rateLimiter = null,
        IMaestroConnectionManager? connectionManager = null,
        IMediator? mediator = null)
    {
        if (rateLimiter is null)
        {
            rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
            rateLimiter.ShouldUpdate(Arg.Any<DateTimeOffset>()).Returns(true);
        }

        connectionManager ??= new MockLocalConnectionManager();
        mediator ??= Substitute.For<IMediator>();

        return new FlightPlanUpdatedHandler(
            sessionManager,
            connectionManager,
            rateLimiter,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
