using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class RemoveRequestHandlerTests(ClockFixture clockFixture)
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
    public async Task WhenAnActiveFlightIsRemoved_ItIsPlacedInThePendingList(bool highPriority)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithRunway("34L")
            .FromDepartureAirport()
            .HighPriority(highPriority)
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(15))
            .WithLandingEstimate(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial state
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        instance.Session.Sequence.FindFlight("QFA123").ShouldBeNull("flight should be removed from the sequence");
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should now be first in sequence");

        var pendingFlight = instance.Session.PendingFlights.SingleOrDefault(f => f.Callsign == "QFA123");
        pendingFlight.ShouldNotBeNull();
        pendingFlight.IsFromDepartureAirport.ShouldBeTrue();
        pendingFlight.IsHighPriority.ShouldBe(highPriority);
    }

    [Fact]
    public async Task WhenADesequencedFlightIsRemoved_ItIsPlacedInThePendingList()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.DeSequencedFlights.Add(flight1);

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var sessionDto = instance.Session.Snapshot();
        sessionDto.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sessionDto.DeSequencedFlights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in desequenced list");
        sessionDto.Sequence.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        // Configure trajectory service with TTG = 12 minutes for both flights
        // flight1: FF=-2, TTG=12, Landing=+10
        // flight2: FF=-1, TTG=12, Landing=+11
        var trajectoryService = new MockTrajectoryService(TimeSpan.FromMinutes(12));

        // Create two flights where flight2 is delayed behind flight1
        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(12)))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFixEstimate(now.AddMinutes(-1))
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(12)))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial state
        flight1.LandingTime.ShouldBe(now.AddMinutes(10), "flight1 lands at FF + TTG = -2 + 12");
        flight2.LandingTime.ShouldBe(now.AddMinutes(13), "flight2 delayed behind flight1 by acceptance rate");

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // After removing flight1, flight2 should be recalculated to land at its estimate (T+11)
        // since there's no longer a conflict with flight1
        flight2.LandingTime.ShouldBe(now.AddMinutes(11),
            "flight2 should be recalculated to land at its estimate after flight1 is removed");
    }

    [Fact]
    public async Task WhenRemovingAFlightThatDoesNotExist_AnExceptionIsThrown()
    {
        // Arrange
        var (instanceManager, _, _, sequence) = new InstanceBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new RemoveRequest("YSSY", "NONEXISTENT");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("not found", Case.Insensitive);
    }

    [Fact]
    public async Task WhenRemovingAFlightFromThePendingList_AnExceptionIsThrown()
    {
        // Arrange
        var (instanceManager, instance, _, sequence) = new InstanceBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(new PendingFlight("QFA123", IsFromDepartureAirport: false, IsHighPriority: false));

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("not found", Case.Insensitive);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new RemoveRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights.ShouldContain(flight, "Flight should remain in sequence when relaying to master");
        instance.Session.PendingFlights.ShouldNotContain(f => f.Callsign == "QFA123", "Flight should not be added to pending list when relaying to master");
    }

    RemoveRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence)
    {
        var mediator = Substitute.For<IMediator>();

        return new RemoveRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());
    }
}
