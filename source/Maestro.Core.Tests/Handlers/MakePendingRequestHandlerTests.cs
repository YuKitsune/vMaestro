using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
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

public class MakePendingRequestHandlerTests(ClockFixture clockFixture)
{
    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

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
            .Build();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenMakingFlightPending_ItIsRemovedFromTheSequenceAndAddedToPendingList(bool highPriority)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithRunway(DefaultRunway)
            .HighPriority(highPriority)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var session = await sessionManager.GetSession(sequence.AirportIdentifier, CancellationToken.None);
        session.Sequence.FindFlight("QFA123").ShouldBeNull("flight should be removed from the sequence");

        var pendingFlight = session.PendingFlights.SingleOrDefault(f => f.Callsign == "QFA123");
        pendingFlight.ShouldNotBeNull();
        pendingFlight.IsFromDepartureAirport.ShouldBeTrue();
        pendingFlight.IsHighPriority.ShouldBe(highPriority);
    }

    // TODO: Throw an error

    [Fact]
    public async Task WhenFlightNotFound_WarningIsLoggedAndHandlerReturns()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();
        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration).Build();

        var logger = Substitute.For<ILogger>();
        var handler = GetRequestHandler(sessionManager, sequence, logger: logger);
        var request = new MakePendingRequest("YSSY", "NONEXISTENT");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        logger.Received(1).Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", "NONEXISTENT", "YSSY");
    }

    [Fact]
    public async Task WhenFlightIsNotFromDepartureAirport_ExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithRunway(DefaultRunway)
            .Build();

        // Mark flight as NOT from departure airport
        flight.IsFromDepartureAirport = false;

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("departure airport", Case.Insensitive);
    }

    MakePendingRequestHandler GetRequestHandler(ISessionManager sessionManager, Sequence sequence, ILogger? logger = null)
    {
        var mediator = Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();
        return new MakePendingRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            mediator,
            logger);
    }
}
