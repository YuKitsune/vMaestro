using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Sessions;
using Maestro.Core.Sessions.Contracts;
using Maestro.Core.Sessions.Handlers;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class CleanUpFlightsRequestHandlerTests(ClockFixture clockFixture)
{
    readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();

    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    [Fact]
    public async Task WhenNoFlights_NothingIsRemoved()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenNoLandedFlights_NothingIsRemoved()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.Add(TimeSpan.FromMinutes(20)))
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_now.Add(TimeSpan.FromMinutes(10)))
            .WithState(State.Stable)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(2);
        sequence.Flights.ShouldContain(flight1);
        sequence.Flights.ShouldContain(flight2);
    }

    [Fact]
    public async Task WhenFewLandedFlightsWithinTimeout_NothingIsRemoved()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 5)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(25)))
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(5);
    }

    [Fact]
    public async Task WhenMoreThanMaxLandedFlights_ExcessFlightsAreRemoved()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 8)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(25)))
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
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
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var oldFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(35)))
            .WithLandingTime(_now.AddMinutes(-15))
            .WithState(State.Landed)
            .Build();

        var recentFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(25)))
            .WithLandingTime(_now.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(oldFlight, recentFlight))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(1);
        sequence.Flights.ShouldNotContain(oldFlight);
        sequence.Flights.ShouldContain(recentFlight);
    }

    [Fact]
    public async Task WhenLandedFlightExactlyAtTimeout_FlightIsRemoved()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(30)))
            .WithLandingTime(_now.AddMinutes(-10))
            .WithState(State.Landed)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenMixOfLandedAndNonLandedFlights_OnlyLandedFlightsAreAffected()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var unstableFlight = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(_now.Add(TimeSpan.FromMinutes(20)))
            .WithState(State.Unstable)
            .Build();

        var oldLandedFlight = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(40)))
            .WithLandingTime(_now.AddMinutes(-20))
            .WithState(State.Landed)
            .Build();

        var stableFlight = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(_now.Add(TimeSpan.FromMinutes(10)))
            .WithState(State.Stable)
            .Build();

        var recentLandedFlight = new FlightBuilder("QFA4")
            .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(25)))
            .WithLandingTime(_now.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(unstableFlight, oldLandedFlight, stableFlight, recentLandedFlight))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(3);
        sequence.Flights.ShouldContain(unstableFlight);
        sequence.Flights.ShouldContain(stableFlight);
        sequence.Flights.ShouldContain(recentLandedFlight);
        sequence.Flights.ShouldNotContain(oldLandedFlight);
    }

    [Fact]
    public async Task WhenMultipleFlightsExceedBothLimits_AllAreRemoved()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 10)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(40)))
                .WithLandingTime(_now.AddMinutes(-20))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetRequestHandler(sessionManager, airportConfiguration);
        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenNotMaster_DoesNothing()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var landedFlights = Enumerable.Range(1, 8)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithFeederFixEstimate(_now.Subtract(TimeSpan.FromMinutes(25)))
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var logger = Substitute.For<ILogger>();
        var configProvider = new AirportConfigurationProvider([airportConfiguration]);
        var handler =  new CleanUpFlightsRequestHandler(
            new MockSlaveConnectionManager(),
            sessionManager,
            configProvider,
            clockFixture.Instance,
            logger);

        var request = new CleanUpFlightsRequest(airportConfiguration.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(8, "All 8 flights should remain, as slave connections cannot modify the sequence locally");
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
            .Build();
    }

    CleanUpFlightsRequestHandler GetRequestHandler(
        ISessionManager sessionManager,
        AirportConfiguration airportConfiguration)
    {
        var logger = Substitute.For<ILogger>();
        var configProvider = new AirportConfigurationProvider([airportConfiguration]);
        return new CleanUpFlightsRequestHandler(
            new MockLocalConnectionManager(),
            sessionManager,
            configProvider,
            clockFixture.Instance,
            logger);
    }
}
