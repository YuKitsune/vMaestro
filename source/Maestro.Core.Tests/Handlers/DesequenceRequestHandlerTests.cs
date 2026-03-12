using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class DesequenceRequestHandlerTests(ClockFixture clockFixture)
{
    static readonly TimeSpan AcceptanceRate = TimeSpan.FromSeconds(180);

    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    [Fact]
    public async Task TheFlightIsRemovedFromTheSequenceAndAddedToTheDesequencedList()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, session, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        sequence.Flights.Count.ShouldBe(1, "Flight should be in sequence");
        session.DeSequencedFlights.Count.ShouldBe(0, "No desequenced flights initially");

        var mediator = Substitute.For<IMediator>();

        var handler = new DesequenceRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new DesequenceRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(0, "Flight should be removed from sequence");
        session.DeSequencedFlights.Count.ShouldBe(1, "Flight should be added to desequenced list");
        session.DeSequencedFlights[0].Callsign.ShouldBe("QFA1", "Desequenced flight should be QFA1");
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway(DefaultRunway)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway(DefaultRunway)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var originalFlight1LandingTime = flight1.LandingTime;

        var mediator = Substitute.For<IMediator>();

        var handler = new DesequenceRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new DesequenceRequest("YSSY", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "QFA1 should remain unchanged");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(AcceptanceRate), "QFA3 should move forward to take QFA2's place");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, session, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        sequence.Flights.Count.ShouldBe(1, "Flight should be in sequence");

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new DesequenceRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new DesequenceRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights.Count.ShouldBe(1, "Flight should not be removed locally when relaying to master");
        session.DeSequencedFlights.Count.ShouldBe(0, "Flight should not be desequenced locally when relaying to master");
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
}
