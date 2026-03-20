using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
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

public class MakeStableRequestHandlerTests(ClockFixture clockFixture)
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

    [Fact]
    public async Task TheFlightIsMarkedAsStable()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var airportConfiguration = CreateAirportConfiguration();

        var unstableFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(unstableFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new MakeStableRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        unstableFlight.State.ShouldBe(State.Stable, "unstable flight should become stable");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task StablisedFlightsAreUnaffected(State state)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(state)
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new MakeStableRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state, $"flight state should remain {state}");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var airportConfiguration = CreateAirportConfiguration();

        var unstableFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(unstableFlight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new MakeStableRequestHandler(
            instanceManager,
            slaveConnectionManager,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new MakeStableRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        unstableFlight.State.ShouldBe(State.Unstable, "Flight state should not change when relaying to master");
    }

    MakeStableRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence, IClock clock)
    {
        var mediator = Substitute.For<IMediator>();
        return new MakeStableRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            clock,
            mediator,
            Substitute.For<ILogger>());
    }
}
