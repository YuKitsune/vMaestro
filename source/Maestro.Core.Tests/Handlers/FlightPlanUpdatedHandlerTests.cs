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
    public async Task WhenAFlightPlanIsUpdated_AndConnectedToAServer_AndNotMaster_UpdateIsDiscarded()
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
        slaveConnectionManager.Connection.InvokedNotifications.Count.ShouldBe(0, "non-master should not relay notifications");
        session.FlightDataRecords.ShouldNotContainKey("QFA123", "non-master should not update FlightDataRecords");
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFlightIsFromDepartureAirport_AndAirborneAndActive_ActivateFlightRequestIsSent()
    {
        // Arrange - YSCB is configured as a departure airport
        // A departure-airport flight auto-activates once it is airborne and the FDR is Active,
        // matching the rules that apply to any other arrival.
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
            FlightPlanState.Active,
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFlightIsFromDepartureAirport_AndIsOnGround_ActivateFlightRequestIsNotSent()
    {
        // Arrange - YSCB is configured as a departure airport
        // A departure-airport flight that is still parked on the ground must not be auto-activated,
        // even when AutoActivateDepartures is enabled. It stays in the Pending List until airborne.
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var onGroundPosition = new FlightPosition(
            new Coordinate(0, 0),
            0,
            VerticalTrack.Maintaining,
            0,
            true);

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
            FlightPlanState.Active,
            onGroundPosition,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFlightIsFromDepartureAirport_AndPositionIsNull_ActivateFlightRequestIsNotSent()
    {
        // Arrange - YSCB is configured as a departure airport
        // A departure-airport flight with no coupled track (no position) must not be auto-activated.
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
            FlightPlanState.Active,
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
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
    public async Task WhenAFlightPlanIsUpdated_AndAircraftIsOnGround_ActivateFlightRequestIsNotSent()
    {
        // Arrange — simulates a landed flight whose FDR is still Active during taxi.
        // Without this guard, removing the flight then receiving another FDR would reactivate it.
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var onGroundPosition = new FlightPosition(
            new Coordinate(0, 0),
            0,
            VerticalTrack.Maintaining,
            0,
            true);

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-2), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            onGroundPosition,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(-5))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndAllEstimatesAreInThePast_ActivateFlightRequestIsNotSent()
    {
        // Arrange — an airborne FDR whose route is fully overflown must not reactivate.
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-2), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(-20)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(-2))
            ]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndPositionIsNull_ActivateFlightRequestIsNotSent()
    {
        // Arrange — the FDR has no coupled track (typical for aircraft parked at the gate
        // where CoupledTrack is null). Without a live position report we cannot confirm the
        // aircraft is airborne, so an arrival must not auto-activate even if the residual
        // route estimates are future-dated (e.g. after a callsign reuse).
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            Position: null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(50))
            ]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFeederFixHasBeenOverflown_ActivateFlightRequestIsNotSent()
    {
        // Arrange — aircraft is inside the TMA (past the feeder fix) with only the destination
        // fix remaining. It must not auto-activate: it was already removed from the sequence
        // while absorbing delay on final and the controller must reinsert it manually.
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [new FixEstimate("YSSY", clock.UtcNow().AddMinutes(5))]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndFeederFixEstimateIsInThePast_ActivateFlightRequestIsNotSent()
    {
        // Arrange — feeder fix still in the route but its estimate has slipped into the past.
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(-2)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(10))
            ]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenAFlightPlanIsUpdated_AndNoFeederFixInFlightPlan_ActivateFlightRequestIsNotSent()
    {
        // Arrange — route contains no configured feeder fix (e.g. non-standard direct routing).
        var airportConfiguration = GetDefaultAirportConfiguration();
        var clock = clockFixture.Instance;
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();
        var notification = new FlightPlanUpdatedNotification(
            "QFA123", "B738", AircraftCategory.Jet, WakeCategory.Medium,
            "YMML", "YSSY", clock.UtcNow().AddHours(-1), TimeSpan.FromHours(1.5),
            FlightPlanState.Active,
            _position,
            [
                new FixEstimate("SOMEFIX", clock.UtcNow().AddMinutes(30)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(50))
            ]);

        var handler = GetHandler(sessionManager, clock, mediator: mediator);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Send(Arg.Any<ActivateFlightRequest>(), Arg.Any<CancellationToken>());
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
        IMaestroConnectionManager? connectionManager = null,
        IAirportConfigurationProvider? airportConfigurationProvider = null,
        IMediator? mediator = null)
    {
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
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
