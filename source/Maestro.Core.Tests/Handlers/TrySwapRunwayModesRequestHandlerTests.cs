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

public class TrySwapRunwayModesRequestHandlerTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();

    readonly RunwayMode _firstRunwayMode = new(new RunwayModeConfiguration
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
    });

    readonly RunwayMode _secondRunwayMode = new(new RunwayModeConfiguration
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
    });

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
        var request = new TrySwapRunwayModesRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        sequence.NextRunwayMode.ShouldBeNull();
        sequence.LastLandingTimeForCurrentMode.ShouldBeNull();
        sequence.FirstLandingTimeForNewMode.ShouldBeNull();
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
        var request = new TrySwapRunwayModesRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        sequence.NextRunwayMode.ShouldBe(_secondRunwayMode);
        sequence.LastLandingTimeForCurrentMode.ShouldBe(_now.AddMinutes(10));
        sequence.FirstLandingTimeForNewMode.ShouldBe(_now.AddMinutes(15));
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
        var request = new TrySwapRunwayModesRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        sequence.NextRunwayMode.ShouldBe(_secondRunwayMode);
        sequence.LastLandingTimeForCurrentMode.ShouldBe(_now.AddMinutes(-5));
        sequence.FirstLandingTimeForNewMode.ShouldBe(_now.AddMinutes(5));
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
        var request = new TrySwapRunwayModesRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_secondRunwayMode);
        sequence.NextRunwayMode.ShouldBeNull();
        sequence.LastLandingTimeForCurrentMode.ShouldBeNull();
        sequence.FirstLandingTimeForNewMode.ShouldBeNull();
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
        var request = new TrySwapRunwayModesRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.ShouldBe(_firstRunwayMode);
        sequence.NextRunwayMode.ShouldBe(_secondRunwayMode);
        sequence.LastLandingTimeForCurrentMode.ShouldBe(_now.AddMinutes(-10));
        sequence.FirstLandingTimeForNewMode.ShouldBe(_now.AddMinutes(0));
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
