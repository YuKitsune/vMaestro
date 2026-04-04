using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Shouldly;
using Serilog;

namespace Maestro.Core.Tests.Handlers;

public class ChangeFeederFixEstimateHandlerTests(ClockFixture clockFixture)
{
    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    [Fact]
    public async Task WhenChangingFeederFixEstimate_ManualFeederFixShouldBeTrue()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var newEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newEstimate);
        var handler = GetHandler(airportConfiguration, sessionManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ManualFeederFixEstimate.ShouldBeTrue();
        flight.FeederFixEstimate.ShouldBe(newEstimate);
    }

    [Fact]
    public async Task WhenChangingFeederFixEstimate_LandingEstimateShouldBeRecalculated()
    {
        // Arrange
        var timeToGoMinutes = 10;
        var timeToGo = TimeSpan.FromMinutes(timeToGoMinutes);

        var airportConfiguration = CreateAirportConfiguration(timeToGoMinutes);

        var trajectoryService = new TrajectoryService(
            new AirportConfigurationProvider([airportConfiguration]),
            Substitute.For<IPerformanceLookup>(),
            Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway(DefaultRunway)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlight(flight))
            .Build();

        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var expectedLandingEstimate = newFeederFixEstimate.Add(timeToGo);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(airportConfiguration, sessionManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingEstimate.ShouldBe(expectedLandingEstimate);
    }

    [Fact]
    public async Task WhenChangingFeederFixEstimate_SequenceIsRecalculated()
    {
        // Arrange
        var timeToGoMinutes = 10;

        var airportConfiguration = CreateAirportConfiguration(timeToGoMinutes);

        var trajectoryService = new TrajectoryService(
            new AirportConfigurationProvider([airportConfiguration]),
            Substitute.For<IPerformanceLookup>(),
            Substitute.For<ILogger>());

        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(15))
            .WithLandingEstimate(clockFixture.Instance.UtcNow().AddMinutes(25))
            .WithRunway(DefaultRunway)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .WithRunway(DefaultRunway)
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(flight2, flight1)) // QFA2 first, QFA1 second
            .Build();

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1);
        sequence.NumberInSequence(flight1).ShouldBe(2);

        var newFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(5);
        var timeToGo = TimeSpan.FromMinutes(timeToGoMinutes);
        var newLandingEstimate = newFeederFixEstimate.Add(timeToGo);

        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newFeederFixEstimate);
        var handler = GetHandler(airportConfiguration, sessionManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - QFA1 should now be first due to earlier landing estimate
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA1 should be first after feeder fix estimate change");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA2 should be second after QFA1's estimate change");
    }

    [Fact]
    public async Task WhenFlightIsUnstable_ItShouldBeMadeStable()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(airportConfiguration, sessionManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task WhenFlightIsNotUnstable_StateIsRetained(State state)
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var request = new ChangeFeederFixEstimateRequest(
            "YSSY",
            "QFA1",
            clockFixture.Instance.UtcNow().AddMinutes(15));
        var handler = GetHandler(airportConfiguration, sessionManager);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var originalFeederFixEstimate = clockFixture.Instance.UtcNow().AddMinutes(10);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixEstimate)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        // Create a mock connection manager that simulates a slave connection
        var mockConnectionManager = new MockSlaveConnectionManager();

        var newEstimate = clockFixture.Instance.UtcNow().AddMinutes(15);
        var request = new ChangeFeederFixEstimateRequest("YSSY", "QFA1", newEstimate);
        var handler = new ChangeFeederFixEstimateRequestHandler(
            sessionManager,
            mockConnectionManager,
            new AirportConfigurationProvider([airportConfiguration]),
            clockFixture.Instance,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Verify the request was relayed to the master
        mockConnectionManager.Connection.InvokedRequests.ShouldContain(request, "request should have been relayed to master");

        // Verify the flight was NOT modified locally (proving redirection occurred)
        flight.ManualFeederFixEstimate.ShouldBeFalse("flight should not have been modified locally");
        flight.FeederFixEstimate.ShouldBe(originalFeederFixEstimate, "feeder fix estimate should remain unchanged");
    }

    static AirportConfiguration CreateAirportConfiguration(int? timeToGoMinutes = null)
    {
        var builder = new AirportConfigurationBuilder("YSSY")
            .WithRunways(DefaultRunway)
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = DefaultRunway,
                LandingRateSeconds = DefaultLandingRateSeconds,
                FeederFixes = []
            });

        if (timeToGoMinutes.HasValue)
        {
            builder.WithTrajectory("RIVET", DefaultRunway, timeToGoMinutes.Value);
        }

        return builder.Build();
    }

    ChangeFeederFixEstimateRequestHandler GetHandler(
        AirportConfiguration airportConfiguration,
        ISessionManager sessionManager,
        IMediator? mediator = null,
        ILogger? logger = null)
    {
        mediator ??= Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();

        return new ChangeFeederFixEstimateRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            new AirportConfigurationProvider([airportConfiguration]),
            clockFixture.Instance,
            mediator,
            logger);
    }
}
