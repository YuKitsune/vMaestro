using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
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

public class TrySwapConfigurationRequestHandlerTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();

    readonly RunwayMode _firstRunwayMode = new(
        new RunwayModeConfiguration
        {
            Identifier = "FIRST",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = 180
                }
            ]
        },
        TimeSpan.Zero);

    readonly RunwayMode _secondRunwayMode = new(
        new RunwayModeConfiguration
        {
            Identifier = "SECOND",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "16R",
                    LandingRateSeconds = 180
                }
            ]
        },
        TimeSpan.Zero);

    [Fact]
    public async Task WhenNoChangeIsInProgress_NothingHappens()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithRunwayMode(_firstRunwayMode))
            .Build();

        var handler = GetRequestHandler(sessionManager);
        var request = new TrySwapConfigurationRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        sequence.PendingConfigurationChange.ShouldBeNull();
    }

    [Fact]
    public async Task WhenChangeIsInTheFuture_ModesAreNotSwapped()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithRunwayMode(_firstRunwayMode))
            .Build();

        sequence.ChangeRunwayMode(
            _secondRunwayMode,
            _now.AddMinutes(10),
            _now.AddMinutes(15));

        var handler = GetRequestHandler(sessionManager);
        var request = new TrySwapConfigurationRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        var change = sequence.PendingConfigurationChange.ShouldBeOfType<TerminalConfigurationChange>();
        change.NewRunwayMode.ShouldBe(_secondRunwayMode);
        change.LastLandingTimeInPreviousMode.ShouldBe(_now.AddMinutes(10));
        change.FirstLandingTimeInNewMode.ShouldBe(_now.AddMinutes(15));
    }

    [Fact]
    public async Task WhenChangePeriodIsActive_ModesAreNotSwapped()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithRunwayMode(_firstRunwayMode))
            .Build();

        sequence.ChangeRunwayMode(
            _secondRunwayMode,
            _now.AddMinutes(-5),
            _now.AddMinutes(5));

        var handler = GetRequestHandler(sessionManager);
        var request = new TrySwapConfigurationRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        var change = sequence.PendingConfigurationChange.ShouldBeOfType<TerminalConfigurationChange>();
        change.NewRunwayMode.ShouldBe(_secondRunwayMode);
        change.LastLandingTimeInPreviousMode.ShouldBe(_now.AddMinutes(-5));
        change.FirstLandingTimeInNewMode.ShouldBe(_now.AddMinutes(5));
    }

    [Fact]
    public async Task WhenChangePeriodIsComplete_ModesAreSwapped()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithRunwayMode(_firstRunwayMode))
            .Build();

        sequence.ChangeRunwayMode(
            _secondRunwayMode,
            _now.AddMinutes(-10),
            _now.AddMinutes(0));

        var handler = GetRequestHandler(sessionManager);
        var request = new TrySwapConfigurationRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_secondRunwayMode);
        sequence.PendingConfigurationChange.ShouldBeNull();
    }

    [Fact]
    public async Task WhenLandingRatesChangePeriodIsComplete_NewRatesAreApplied()
    {
        // Arrange
        var newRate = TimeSpan.FromSeconds(300);
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithRunwayMode(_firstRunwayMode))
            .Build();

        sequence.ChangeLandingRates(
            new Dictionary<string, TimeSpan> { ["34L"] = newRate },
            _now.AddMinutes(-5));

        var handler = GetRequestHandler(sessionManager);
        var request = new TrySwapConfigurationRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("FIRST", "the runway mode identifier should not change");
        sequence.CurrentRunwayMode.Runways.Single(r => r.Identifier == "34L").AcceptanceRate.ShouldBe(newRate);
        sequence.PendingConfigurationChange.ShouldBeNull();
    }

    [Fact]
    public async Task WhenNotMaster_DoesNothing()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithRunwayMode(_firstRunwayMode))
            .Build();

        sequence.ChangeRunwayMode(
            _secondRunwayMode,
            _now.AddMinutes(-10),
            _now.AddMinutes(0));

        var handler = GetRequestHandler(sessionManager, new MockSlaveConnectionManager());
        var request = new TrySwapConfigurationRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        var change = sequence.PendingConfigurationChange.ShouldBeOfType<TerminalConfigurationChange>();
        change.NewRunwayMode.ShouldBe(_secondRunwayMode);
        change.LastLandingTimeInPreviousMode.ShouldBe(_now.AddMinutes(-10));
        change.FirstLandingTimeInNewMode.ShouldBe(_now.AddMinutes(0));
    }

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunwayMode(
                "DUMMY",
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = 180
                }).Build();
    }

    TrySwapRunwayModesRequestHandler GetRequestHandler(ISessionManager sessionManager, IMaestroConnectionManager? connectionManager = null)
    {
        return new TrySwapRunwayModesRequestHandler(
            connectionManager ?? new MockLocalConnectionManager(),
            sessionManager,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());
    }
}
