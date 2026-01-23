using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class CleanUpFlightsRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly DateTimeOffset _now = clockFixture.Instance.UtcNow();

    [Fact]
    public async Task WhenNoFlights_NothingIsRemoved()
    {
        // Arrange
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenNoLandedFlights_NothingIsRemoved()
    {
        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithState(State.Unstable)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

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
        var landedFlights = Enumerable.Range(1, 5)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(5);
    }

    [Fact]
    public async Task WhenMoreThanMaxLandedFlights_ExcessFlightsAreRemoved()
    {
        // Arrange
        var landedFlights = Enumerable.Range(1, 8)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithLandingTime(_now.AddMinutes(-5))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

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
        var oldFlight = new FlightBuilder("QFA1")
            .WithLandingTime(_now.AddMinutes(-15))
            .WithState(State.Landed)
            .Build();

        var recentFlight = new FlightBuilder("QFA2")
            .WithLandingTime(_now.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(oldFlight, recentFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

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
        var flight = new FlightBuilder("QFA1")
            .WithLandingTime(_now.AddMinutes(-10))
            .WithState(State.Landed)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenMixOfLandedAndNonLandedFlights_OnlyLandedFlightsAreAffected()
    {
        // Arrange
        var unstableFlight = new FlightBuilder("QFA1")
            .WithState(State.Unstable)
            .Build();

        var oldLandedFlight = new FlightBuilder("QFA2")
            .WithLandingTime(_now.AddMinutes(-20))
            .WithState(State.Landed)
            .Build();

        var stableFlight = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .Build();

        var recentLandedFlight = new FlightBuilder("QFA4")
            .WithLandingTime(_now.AddMinutes(-5))
            .WithState(State.Landed)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(unstableFlight, oldLandedFlight, stableFlight, recentLandedFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

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
        var landedFlights = Enumerable.Range(1, 10)
            .Select(i => new FlightBuilder($"QFA{i}")
                .WithLandingTime(_now.AddMinutes(-20))
                .WithState(State.Landed)
                .Build())
            .ToArray();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithClock(clockFixture.Instance)
                .WithFlightsInOrder(landedFlights))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new CleanUpFlightsRequest(airportConfigurationFixture.Instance.Identifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    CleanUpFlightsRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager)
    {
        var logger = Substitute.For<ILogger>();
        var configProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        return new CleanUpFlightsRequestHandler(instanceManager, configProvider, clockFixture.Instance, logger);
    }
}
