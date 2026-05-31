using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
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
    public async Task WhenAFlightPlanIsUpdated_TheSessionIsUpdated()
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
            FlightPlanState.Active,
            _position,
            [
                new FixEstimate("RIVET", feederFixEstimate),
                new FixEstimate("YSSY", landingEstimate)
            ]);

        var handler = GetHandler(sessionManager, clock);

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
    public async Task WhenAFlightPlanIsUpdated_AndFdrIsAlreadyKnown_FdrIsUpdated()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        session.FlightDataRecords["QFA123"] = new FlightDataRecord(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            null,
            TimeSpan.FromHours(1),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20))],
            clock.UtcNow().AddSeconds(-10));

        var newFeederFixEstimate = clock.UtcNow().AddMinutes(15);
        var notification = new FlightPlanUpdatedNotification(
            "QFA123",
            "B744",
            AircraftCategory.Jet,
            WakeCategory.Heavy,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", newFeederFixEstimate)]);

        var handler = GetHandler(sessionManager, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        session.FlightDataRecords.TryGetValue("QFA123", out var record).ShouldBeTrue();
        record.AircraftType.ShouldBe("B744");
        record.WakeCategory.ShouldBe(WakeCategory.Heavy);
        record.Estimates.ShouldContain(e => e.FixIdentifier == "RIVET" && e.Estimate == newFeederFixEstimate);
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndTooSoonSinceLastUpdate_TheUpdateIsRateLimited()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        var originalLastSeen = clock.UtcNow().AddSeconds(-5);
        session.FlightDataRecords["QFA123"] = new FlightDataRecord(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", null,
            TimeSpan.FromHours(1),
            FlightPlanState.Active, _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(20))],
            originalLastSeen);

        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdate(Arg.Any<DateTimeOffset>()).Returns(false);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B744", AircraftCategory.Jet, WakeCategory.Heavy,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, rateLimiter: rateLimiter);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var record = session.FlightDataRecords["QFA123"];
        record.LastSeen.ShouldBe(originalLastSeen, "FlightDataRecord should not update when rate-limited");
        record.AircraftType.ShouldBe("B738", "FlightDataRecord should not update when rate-limited");
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFlightIsActive_ActivateFlightRequestIsSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFlightIsPreactive_ActivateFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            FlightPlanState.Preactive,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndAlreadyActivated_ActivateFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(new FlightBuilder("QFA123")
                .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
                .WithLandingEstimate(clock.UtcNow().AddMinutes(40))
                .Build()))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndAlreadyDesequenced_ActivateFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        session.DeSequencedFlights.Add(new FlightBuilder("QFA123")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(40))
            .Build());

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndConnectedToAServer_NotificationIsRelayedToMaster()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, session, _) = new SessionBuilder(airportConfiguration).Build();

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();
        var handler = GetHandler(sessionManager, clock, connectionManager: slaveConnectionManager, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedNotifications.Count.ShouldBe(1, "notification should be relayed to master");
        slaveConnectionManager.Connection.InvokedNotifications[0].ShouldBe(notification);
        session.FlightDataRecords.ShouldNotContainKey("QFA123", "slave should not update FlightDataRecords locally");
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFlightIsFromDepartureAirport_ActivateFlightRequestIsSent()
    {
        // Arrange - YSCB is configured as a departure airport
        // Ensure flights from departure airports auto-activate by themselves from departure airports once airborne
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "JST425",
            "A320",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YSCB",
            "YSSY",
            clock.UtcNow(),
            TimeSpan.FromMinutes(35),
            FlightPlanState.Preactive,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAutoActivateDeparturesIsDisabled_AndFlightIsFromDepartureAirport_ActivateFlightRequestIsNotSent()
    {
        // Arrange - YSCB is configured as a departure airport, but AutoActivateDepartures is disabled
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithFeederFixes("RIVET", "BOREE", "WELSH")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithDepartureAirport("YSCB", [new AllAircraftTypesDescriptor()], 15)
            .WithAutoActivateDepartures(false)
            .Build();

        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfiguration(Arg.Any<string>()).Returns(airportConfiguration);

        var notification = new FlightPlanUpdatedNotification(
            "JST425", "A320", AircraftCategory.Jet, WakeCategory.Medium,
            "YSCB", "YSSY", clock.UtcNow(), TimeSpan.FromMinutes(35),
            FlightPlanState.Preactive,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, airportConfigurationProvider: airportConfigurationProvider, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndLandingEstimateExceedsMaxLeadTime_ActivateFlightRequestIsNotSent()
    {
        // Arrange - MaximumAutoActivationLeadTimeMinutes set to 60; landing estimate is 90 min out
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithFeederFixes("RIVET", "BOREE", "WELSH")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithMaximumAutoActivationLeadTimeMinutes(60)
            .Build();

        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfiguration(Arg.Any<string>()).Returns(airportConfiguration);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(70)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(90))
            ]);

        var handler = GetHandler(sessionManager, clock, airportConfigurationProvider: airportConfigurationProvider, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndEstimatedFlightTimeExceedsMinimum_ActivateFlightRequestIsSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfiguration(Arg.Any<string>()).Returns(airportConfiguration);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromMinutes(31),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sessionManager, clock, airportConfigurationProvider: airportConfigurationProvider, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndEstimatedFlightTimeIsBelowMinimum_ActivateFlightRequestIsNotSent()
    {
        // Arrange
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfiguration(Arg.Any<string>()).Returns(airportConfiguration);

        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddMinutes(-20), TimeSpan.FromMinutes(20),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10))]);

        var handler = GetHandler(sessionManager, clock, airportConfigurationProvider: airportConfigurationProvider, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    FlightPlanUpdatedHandler GetHandler(
        ISessionManager sessionManager,
        IClock clock,
        IFlightUpdateRateLimiter? rateLimiter = null,
        IMaestroConnectionManager? connectionManager = null,
        IAirportConfigurationProvider? airportConfigurationProvider = null,
        IMediator? mediator = null)
    {
        if (rateLimiter is null)
        {
            rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
            rateLimiter.ShouldUpdate(Arg.Any<DateTimeOffset>()).Returns(true);
        }

        if (airportConfigurationProvider is null)
        {
            airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
            airportConfigurationProvider.GetAirportConfiguration(Arg.Any<string>()).Returns(GetDefaultAirportConfiguration());
        }

        connectionManager ??= new MockLocalConnectionManager();
        mediator ??= Substitute.For<IMediator>();

        return new FlightPlanUpdatedHandler(
            sessionManager,
            connectionManager,
            airportConfigurationProvider,
            rateLimiter,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
