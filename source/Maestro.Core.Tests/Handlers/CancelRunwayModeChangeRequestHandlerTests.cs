using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class CancelRunwayModeChangeRequestHandlerTests(ClockFixture clockFixture)
{
    const int DefaultDependencyRateSeconds = 30;
    const int DefaultOffModeSeconds = 300;

    [Fact]
    public async Task WhenCancellingModeChange_AndNoModeChangeIsPresent_NothingHappens()
    {
        // Arrange
        var airportConfiguration = BuildAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new CancelRunwayModeChangeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        sequence.NextRunwayMode.ShouldBeNull("precondition: no mode change should be pending");

        // Act
        await handler.Handle(new CancelRunwayModeChangeRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA", "current mode should be unchanged");
        sequence.NextRunwayMode.ShouldBeNull();
        await mediator.DidNotReceive().Publish(Arg.Any<SessionUpdatedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenCancellingModeChange_ModeChangeIsCancelled()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = BuildAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

        var mediator = Substitute.For<IMediator>();

        await ScheduleModeChange(sessionManager, airportConfiguration, mediator, now.AddMinutes(20), now.AddMinutes(25));

        sequence.NextRunwayMode.ShouldNotBeNull("precondition: mode change should be pending");
        mediator.ClearReceivedCalls();

        var handler = new CancelRunwayModeChangeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        // Act
        await handler.Handle(new CancelRunwayModeChangeRequest("YSSY"), CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA", "current mode should be unchanged");
        sequence.NextRunwayMode.ShouldBeNull();
        sequence.LastLandingTimeForCurrentMode.ShouldBeNull();
        sequence.FirstLandingTimeForNewMode.ShouldBeNull();
    }

    [Fact]
    public async Task WhenCancellingModeChange_FlightsInCancelledModeAreRescheduled()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = BuildAirportConfiguration();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        // flight2's ETA falls in the gap zone (T+22, between lastLanding T+20 and firstLanding T+25),
        // so scheduling the mode change pushes it backward to firstLandingTimeForNewMode on 16R.
        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(22))
            .WithLandingTime(now.AddMinutes(22))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        await ScheduleModeChange(sessionManager, airportConfiguration, mediator, now.AddMinutes(20), firstLandingTimeForNewMode);

        flight2.AssignedRunwayIdentifier.ShouldBe("16R", "precondition: flight2 should be in new mode zone");
        flight2.LandingTime.ShouldBe(firstLandingTimeForNewMode, "precondition: flight2 should be delayed to firstLandingTimeForNewMode");

        var handler = new CancelRunwayModeChangeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        // Act
        await handler.Handle(new CancelRunwayModeChangeRequest("YSSY"), CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "flight1 should remain on 34L");
        flight2.AssignedRunwayIdentifier.ShouldBe("34L", "flight2 should be reassigned to 34L after cancel");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "flight2 delay should be removed after cancel");
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        // Arrange
        var airportConfiguration = BuildAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new CancelRunwayModeChangeRequestHandler(
            sessionManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new CancelRunwayModeChangeRequest("YSSY");
        var originalRunwayMode = sequence.CurrentRunwayMode.Identifier;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "the relayed request should match the original");
        sequence.CurrentRunwayMode.Identifier.ShouldBe(originalRunwayMode, "local sequence should not be modified when relaying");
    }

    Task ScheduleModeChange(
        ISessionManager sessionManager,
        AirportConfiguration airportConfiguration,
        IMediator mediator,
        DateTimeOffset lastLandingTimeForOldMode,
        DateTimeOffset firstLandingTimeForNewMode)
    {
        var handler = new ChangeRunwayModeRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        return handler.Handle(
            new ChangeRunwayModeRequest("YSSY", BuildNewModeDto(), lastLandingTimeForOldMode, firstLandingTimeForNewMode),
            CancellationToken.None);
    }

    static AirportConfiguration BuildAirportConfiguration() =>
        new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R", "16L", "16R")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] })
            .WithRunwayMode("16IVA",
                new RunwayConfiguration { Identifier = "16L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["BOREE"] },
                new RunwayConfiguration { Identifier = "16R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = ["RIVET"] })
            .Build();

    static RunwayModeDto BuildNewModeDto() =>
        new RunwayModeDto(
            "16IVA",
            [
                new RunwayDto("16L", string.Empty, 180, []),
                new RunwayDto("16R", string.Empty, 180, [])
            ],
            DefaultDependencyRateSeconds,
            DefaultOffModeSeconds);
}
