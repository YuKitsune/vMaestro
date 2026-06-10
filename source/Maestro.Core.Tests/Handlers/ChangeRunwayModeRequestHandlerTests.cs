using Maestro.Contracts.Flights;
using Maestro.Contracts.Runway;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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

public class ChangeRunwayModeRequestHandlerTests(ClockFixture clockFixture)
{
    const int DefaultDependencyRateSeconds = 30;
    const int DefaultOffModeSeconds = 300;

    [Fact]
    public async Task WhenStartTimeIsNowOrEarlier_CurrentRunwayModeIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34R")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial runway mode
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA");

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now,
            now);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("16IVA");
        sequence.PendingConfigurationChange.ShouldBeNull();
    }

    [Fact]
    public async Task WhenStartTimeIsInTheFuture_NextRunwayModeAndStartTimesAreSet()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34R")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial runway mode
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA");

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var lastLandingTimeForOldMode = now.AddMinutes(20);
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTimeForOldMode,
            firstLandingTimeForNewMode);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA", "Current mode should remain unchanged");
        var change = sequence.PendingConfigurationChange.ShouldBeOfType<TerminalConfigurationChange>();
        change.NewRunwayMode.Identifier.ShouldBe("16IVA");
        change.LastLandingTimeInPreviousMode.ShouldBe(lastLandingTimeForOldMode);
        change.FirstLandingTimeInNewMode.ShouldBe(firstLandingTimeForNewMode);
    }

    [Fact]
    public async Task FlightsScheduledToLandAfterNewMode_ReAssignedToNewRunways()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(30))
            .WithLandingTime(now.AddMinutes(30))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithLandingEstimate(now.AddMinutes(33))
            .WithLandingTime(now.AddMinutes(33))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var lastLandingTimeForOldMode = now.AddMinutes(20);
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTimeForOldMode,
            firstLandingTimeForNewMode);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "QFA1 lands before mode change, should remain on 34L");
        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "QFA2 lands before mode change, should remain on 34R");
        flight3.AssignedRunwayIdentifier.ShouldBe("16R", "QFA3 lands after mode change, should be reassigned to 16R");
        flight4.AssignedRunwayIdentifier.ShouldBe("16L", "QFA4 lands after mode change, should be reassigned to 16L");
    }

    [Fact]
    public async Task FlightsScheduledToLandAfterNewMode_AreRescheduled()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(24))
            .WithLandingTime(now.AddMinutes(24))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithLandingEstimate(now.AddMinutes(27))
            .WithLandingTime(now.AddMinutes(27))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        // New mode with 240-second (4-minute) acceptance rate instead of 180 seconds (3 minutes)
        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 240, []),
                new RunwayDto("16R", string.Empty, 240, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var lastLandingTimeForOldMode = now.AddMinutes(20);
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTimeForOldMode,
            firstLandingTimeForNewMode);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "QFA1 lands before mode change, landing time should remain unchanged");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "QFA2 lands before mode change, landing time should remain unchanged");

        flight3.LandingTime.ShouldBeGreaterThanOrEqualTo(firstLandingTimeForNewMode, "QFA3 should be delayed until after the mode change");
        flight4.LandingTime.ShouldBeGreaterThanOrEqualTo(flight3.LandingTime.AddSeconds(240), "QFA4 should maintain 240-second separation from QFA3");

        sequence.Flights[0].Callsign.ShouldBe("QFA1", "Order should remain: QFA1 first");
        sequence.Flights[1].Callsign.ShouldBe("QFA2", "Order should remain: QFA2 second");
        sequence.Flights[2].Callsign.ShouldBe("QFA3", "Order should remain: QFA3 third");
        sequence.Flights[3].Callsign.ShouldBe("QFA4", "Order should remain: QFA4 fourth");
    }

    [Fact]
    public async Task FrozenFlights_AreNotAffected()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithState(State.Unstable)
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithLandingEstimate(now.AddMinutes(19))
            .WithLandingTime(now.AddMinutes(19))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .WithState(State.Unstable)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now,
            now);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "Frozen flight QFA1 should remain on 34L");
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Frozen flight QFA1 landing time should remain unchanged");

        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "Frozen flight QFA2 should remain on 34R");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "Frozen flight QFA2 landing time should remain unchanged");

        flight3.AssignedRunwayIdentifier.ShouldBe("16R", "Non-frozen flight QFA3 should be reassigned to 16R");
        flight4.AssignedRunwayIdentifier.ShouldBe("16L", "Non-frozen flight QFA4 should be reassigned to 16L");
    }

    [Fact]
    public async Task WhenBoundaryMovesForward_FlightsInGapZoneReprocessedAgainstOldMode()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        // flight1 lands before the mode change boundary and is unaffected throughout
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        // flight2 has an ETA in the gap zone (T+22, between lastLanding T+20 and firstLanding T+25),
        // so the first mode change will push it backward to firstLandingTimeForNewMode.
        // When the boundary moves to a later time (T+30/T+35), flight2 is before the new boundary
        // and gets re-processed against the old mode, returning to its original ETA.
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(22))
            .WithLandingTime(now.AddMinutes(22))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var firstLandingTimeForNewMode = now.AddMinutes(25);

        // First request: flight2's ETA (T+22) falls in the gap zone, so it is pushed backward to firstLandingTimeForNewMode
        var firstRequest = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now.AddMinutes(20),
            firstLandingTimeForNewMode);

        await handler.Handle(firstRequest, CancellationToken.None);

        flight2.AssignedRunwayIdentifier.ShouldBe("16R", "flight2 should be assigned to the new mode runway after first request");
        flight2.LandingTime.ShouldBe(firstLandingTimeForNewMode, "flight2 should be delayed to firstLandingTimeForNewMode");

        // Second request: boundary moves to a later time (T+30/T+35) — flight2 is now before the new boundary,
        // so it must be reprocessed against the old mode and its delay removed
        var secondRequest = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now.AddMinutes(30),
            now.AddMinutes(35));

        await handler.Handle(secondRequest, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "flight1 should remain on 34L");
        flight2.AssignedRunwayIdentifier.ShouldBe("34L", "flight2 is before the new boundary and should be reassigned to 34L");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "delay should be removed as flight2 is no longer affected by the mode change");
    }

    [Fact]
    public async Task FlightsInNewMode_SeparatedByDependencyRate()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        const int dependencyRateSeconds = 90;

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode(new RunwayModeConfiguration
            {
                Identifier = "16IVA",
                DependencyRateSeconds = dependencyRateSeconds,
                Runways =
                [
                    new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                    new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] }
                ]
            })
            .Build();

        // Two flights with the same ETA landing in the new mode on different runways
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(27))
            .WithLandingTime(now.AddMinutes(27))
            .WithFeederFix("RIVET")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(27))
            .WithLandingTime(now.AddMinutes(27))
            .WithFeederFix("BOREE")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            dependencyRateSeconds,
            DefaultOffModeSeconds);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now.AddMinutes(20),
            now.AddMinutes(25));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("16R", "QFA1 (RIVET) should be assigned to 16R in the new mode");
        flight2.AssignedRunwayIdentifier.ShouldBe("16L", "QFA2 (BOREE) should be assigned to 16L in the new mode");

        var actualSeparation = flight2.LandingTime - flight1.LandingTime;
        actualSeparation.ShouldBeGreaterThanOrEqualTo(
            TimeSpan.FromSeconds(dependencyRateSeconds),
            "flights on different runways in the new mode should be separated by the dependency rate");
    }

    [Fact]
    public async Task StableFlights_WithOldModeRunway_AreReassignedToNewMode()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        const int acceptanceRateSeconds = 180;

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = acceptanceRateSeconds, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = acceptanceRateSeconds, FeederFixes = ["BOREE"] })
            .WithRunwayMode(new RunwayModeConfiguration
            {
                Identifier = "16IVA",
                Runways =
                [
                    new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = acceptanceRateSeconds, FeederFixes = ["BOREE"] },
                    new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = acceptanceRateSeconds, FeederFixes = ["RIVET"] }
                ]
            })
            .Build();

        // flight1 is unstable with a BOREE feeder fix — gets assigned to 16L
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(27))
            .WithLandingTime(now.AddMinutes(27))
            .WithFeederFix("BOREE")
            .Build();

        // flight2 is stable on 34L (old mode) — should be re-assigned to the new mode on mode change
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(27))
            .WithLandingTime(now.AddMinutes(27))
            .WithRunway("34L")
            .WithApproachType(string.Empty)
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, acceptanceRateSeconds, []),
                new RunwayDto("16R", string.Empty, acceptanceRateSeconds, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now.AddMinutes(20),
            now.AddMinutes(25));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var firstLandingTimeForNewMode = now.AddMinutes(25);
        flight1.AssignedRunwayIdentifier.ShouldBe("16L", "QFA1 (BOREE) should be assigned to 16L in the new mode");
        flight2.AssignedRunwayIdentifier.ShouldBeOneOf("16L", "16R",
            "stable QFA2 should be re-assigned to the new mode — its old 34L runway is no longer valid");
        flight2.LandingTime.ShouldBeGreaterThanOrEqualTo(firstLandingTimeForNewMode,
            "QFA2 must land at or after the new mode start time");
    }

    [Fact]
    public async Task FlightDelayedByModeChange_RemainsDelayedWhenRunwayIsManuallyChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();


        // flight1 lands at T+30, well before the gap zone.
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(30))
            .WithLandingTime(now.AddMinutes(30))
            .WithFeederFix("RIVET")
            .WithRunway("34L")
            .Build();

        // flight2 has ETA T+31; with a 3-minute (180 s) acceptance rate the scheduler places
        // it at T+33, which falls in the gap zone [T+32, T+35].
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(31))
            .WithLandingTime(now.AddMinutes(31))
            .WithFeederFix("RIVET")
            .WithRunway("34L")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var modeChangeHandler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var lastLandingTime = now.AddMinutes(32);
        var firstLandingTime = now.AddMinutes(35);

        var modeChangeRequest = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTime,
            firstLandingTime);

        // Act: Schedule the mode change
        await modeChangeHandler.Handle(modeChangeRequest, CancellationToken.None);

        // Assert: flight1 has no delay and is in the current mode
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "flight1 lands before the mode change boundary and should have no delay");
        flight1.AssignedRunwayIdentifier.ShouldBeOneOf("34L", "34R");

        // Assert: flight2 is delayed to firstLandingTime and assigned to the new mode
        flight2.LandingTime.ShouldBe(firstLandingTime, "flight2 falls in the gap zone and should be delayed to firstLandingTime");
        flight2.AssignedRunwayIdentifier.ShouldBeOneOf("16L", "16R");

        var runwayChangeHandler = new ChangeRunwayRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            new MockTrajectoryService(),
            Substitute.For<IClock>(),
            mediator,
            Substitute.For<ILogger>());

        // Act: Change flight2 to the other runway within the new mode (e.g. 16R -> 16L)
        var otherNewModeRunway = flight2.AssignedRunwayIdentifier == "16L" ? "16R" : "16L";
        await runwayChangeHandler.Handle(
            new ChangeRunwayRequest("YSSY", "QFA2", otherNewModeRunway),
            CancellationToken.None);

        // Assert: delay must be preserved when switching between new-mode runways
        flight2.LandingTime.ShouldBe(firstLandingTime, "changing to another runway in the new mode should not remove the mode-change delay");

        // Act: change flight2 to a runway that is not in the new mode (34R)
        await runwayChangeHandler.Handle(
            new ChangeRunwayRequest("YSSY", "QFA2", "34R"),
            CancellationToken.None);

        // Assert: delay must be preserved even when assigned to an off-mode runway
        flight2.LandingTime.ShouldBe(firstLandingTime, "changing to an off-mode runway should not remove the mode-change delay");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfiguration]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            slaveConnectionManager,
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now,
            now);

        var originalRunwayMode = sequence.CurrentRunwayMode.Identifier;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.CurrentRunwayMode.Identifier.ShouldBe(originalRunwayMode, "Local sequence should not be modified when relaying to master");
    }
}
