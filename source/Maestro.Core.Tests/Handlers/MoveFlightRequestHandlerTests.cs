using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
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

public class MoveFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task FlightIsPositionedBasedOnTargetTime()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", ["34L"], newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "Second flight should be QFA3 (moved)");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "Third flight should be QFA2");
    }

    [Fact]
    public async Task RunwayIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", ["34R"], newLandingTime);

        flight.AssignedRunwayIdentifier.ShouldBe("34L", "Flight should initially be on 34L");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R", "Flight runway should be updated to 34R");
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public async Task TheSequenceIsRecalculated(State state)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        var newLandingTime = now.AddMinutes(11);
        var request = new MoveFlightRequest("YSSY", "QFA3", ["34L"], newLandingTime);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "First flight should remain unchanged");
        flight3.LandingTime.ShouldBe(flight3.LandingEstimate, "Moved flight should have no delay (STA = ETA)");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Third flight should be delayed to maintain separation from the moved flight");
    }

    [Fact]
    public async Task WhenEstimateIsAheadOfFrozenFlights_FrozenFlightsAreNotMoved()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(5))
            .WithLandingTime(now.AddMinutes(19))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", ["34L"], newLandingTime);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Frozen flight QFA1 should remain unchanged");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "Frozen flight QFA2 should remain unchanged");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "Second flight should be QFA3 (moved between frozen flights)");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "Third flight should be QFA2");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Moved flight should be delayed behind the first frozen flight");
    }

    // TODO: What if ETA is behind frozen flight? Exception? Move backwards?

    [Fact]
    public async Task UnstableFlightsBecomeStable()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        flight.State.ShouldBe(State.Unstable, "Flight should initially be Unstable");

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", ["34L"], newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable, "Unstable flight should become Stable after being moved");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task StableFlightsDoNotChangeState(State state)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        flight.State.ShouldBe(state, $"Flight should initially be {state}");

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", ["34L"], newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state, $"Flight state should remain {state} after being moved");
    }

    [Fact]
    public async Task InsufficientSpaceBetweenFrozenFlights_ThrowsException()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(18))
            .WithLandingTime(now.AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", ["34L"], newLandingTime);

        // Act & Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();
        var clock = clockFixture.Instance;

        var handler = new MoveFlightRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            clock,
            Substitute.For<ILogger>());

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", ["34L"], newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "Sequence should not be modified locally when relaying to master");
        sequence.Flights[1].Callsign.ShouldBe("QFA2", "Sequence should not be modified locally when relaying to master");
        sequence.Flights[2].Callsign.ShouldBe("QFA3", "Sequence should not be modified locally when relaying to master");
    }

    MoveFlightRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence)
    {
        var mediator = Substitute.For<IMediator>();
        var clock = clockFixture.Instance;
        return new MoveFlightRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
