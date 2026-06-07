using Maestro.Contracts.Runway;
using Maestro.Contracts.Sessions;
using Maestro.Core;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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

public class ChangeLandingRatesRequestHandlerTests(ClockFixture clockFixture)
{
    [Fact]
    public async Task SchedulesLandingRatesChange()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var newRate = TimeSpan.FromSeconds(300);
        var changeTime = now.AddMinutes(15);
        var airportConfiguration = BuildAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var handler = GetRequestHandler(sessionManager, mediator);

        var request = new ChangeLandingRatesRequest(
            "YSSY",
            new Dictionary<string, TimeSpan> { ["34L"] = newRate },
            changeTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var pending = sequence.PendingConfigurationChange.ShouldBeOfType<LandingRatesChange>();
        pending.ChangeTime.ShouldBe(changeTime);
        pending.NewLandingRates["34L"].ShouldBe(newRate);
        sequence.CurrentRunwayMode.Runways.Single(r => r.Identifier == "34L").AcceptanceRate.ShouldBe(TimeSpan.FromSeconds(180),
            "current mode is unchanged until the change time is reached");

        await mediator.Received().Publish(Arg.Any<SessionUpdatedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenRatesAreForRunwayNotInCurrentMode_Throws()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = BuildAirportConfiguration();
        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(sessionManager, Substitute.For<IMediator>());

        var request = new ChangeLandingRatesRequest(
            "YSSY",
            new Dictionary<string, TimeSpan> { ["16L"] = TimeSpan.FromSeconds(300) },
            now.AddMinutes(15));

        // Act / Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        // Arrange
        var airportConfiguration = BuildAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var handler = new ChangeLandingRatesRequestHandler(
            sessionManager,
            slaveConnectionManager,
            clockFixture.Instance,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());

        var request = new ChangeLandingRatesRequest(
            "YSSY",
            new Dictionary<string, TimeSpan> { ["34L"] = TimeSpan.FromSeconds(300) },
            clockFixture.Instance.UtcNow().AddMinutes(15));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "the relayed request should match the original");
        sequence.PendingConfigurationChange.ShouldBeNull("local sequence should not be modified when relaying");
    }

    ChangeLandingRatesRequestHandler GetRequestHandler(ISessionManager sessionManager, IMediator mediator) =>
        new(
            sessionManager,
            new MockLocalConnectionManager(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

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
}
