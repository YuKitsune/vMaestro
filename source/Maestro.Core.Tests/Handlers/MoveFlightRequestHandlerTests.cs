using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
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
    public async Task TargetLandingTimeIsSet()
    {
        var now = clockFixture.Instance.UtcNow();
        var landingEstimate = now.AddMinutes(10);
        var originalLandingTime = now.AddMinutes(10);

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithLandingTime(originalLandingTime)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["34L"],
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingEstimate.ShouldBe(landingEstimate, "Moving a flight should not affect the landing estimate");
        flight.TargetLandingTime.ShouldBe(newLandingTime, "Moving a flight should set the TargetLandingTime");
        flight.LandingTime.ShouldBe(newLandingTime, "Moving a flight should schedule the it at the TargetLandingTime");
    }

    [Fact]
    public async Task RunwayIsSet()
    {
        var now = clockFixture.Instance.UtcNow();
        var landingEstimate = now.AddMinutes(10);
        var originalLandingTime = now.AddMinutes(10);

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(landingEstimate)
            .WithLandingTime(originalLandingTime)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["34R"],
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R", "Moving a flight should assign the desired runway");
    }

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        flight.State.ShouldBe(state, $"Flight should initially be {state}");

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA1", ["34L"], newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state, $"Flight state should remain {state} after being moved");
    }

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

        var handler = GetRequestHandler(instanceManager);

        var newLandingTime = now.AddMinutes(12);
        var request = new MoveFlightRequest("YSSY", "QFA3", ["34L"], newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should remain in it's current position");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "The moved flight should be moved forward one position");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "The original flight in position 2 should move behind the moved flight");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task MovedFlightIsDelayedBehindLeadingFlight(State leadingFlightState)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(leadingFlightState)
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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        // Move QFA2 to _just_ behind QFA1
        var newLandingTime = now.AddMinutes(11);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            ["34L"],
            newLandingTime);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Leading flight should remain unchanged");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Moved flight should be moved forward to the target time, then delayed for separation with the leading flight");
        flight3.LandingTime.ShouldBe(flight3.LandingEstimate, "Third flight should be unaffected");
    }

    [Fact]
    public async Task TrailingFlightsAreDelayedBehindMovedFlight()
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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        // Move QFA2 to _just_ in front of QFA3
        var newLandingTime = now.AddMinutes(19);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            ["34L"],
            newLandingTime);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "First flight should remain unchanged");
        flight2.LandingTime.ShouldBe(newLandingTime, "Moved flight should be moved back to the target time");
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Third flight should be delayed behind the moved flight");
    }

    [Fact]
    public async Task CannotMoveInFrontOfFrozenFlightWithoutEnoughSpace()
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
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var newLandingTime = now.AddMinutes(9);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            ["34L"],
            newLandingTime);

        // Act & Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task CanMoveInFrontOfFrozenFlightWithEnoughSpace()
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
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var newLandingTime = now.AddMinutes(5);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA2",
            ["34L"],
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.LandingTime.ShouldBe(newLandingTime, "Moved flight should move to the target time");
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "Frozen flight should be unaffected");
    }

    [Fact]
    public async Task CannotMoveBetweenFrozenFlightsWithoutEnoughSpace()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(18))
            .WithLandingTime(now.AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        var newLandingTime = now.AddMinutes(13);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            newLandingTime);

        // Act & Assert
        await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task CanMoveBetweenFrozenFlightsWithEnoughSpace()
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithRunway("34L")
            .WithState(State.Stable)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var handler = GetRequestHandler(instanceManager);

        // Move the flight perfectly between the two frozen flights
        var newLandingTime = now.AddMinutes(13);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "First frozen flight should remain unchanged");
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Moved flight should be sequenced between the two frozen flight");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "Second frozen flight should remain unchanged");
    }

    [Fact]
    public async Task RedirectsToMaster()
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

        var handler = GetRequestHandler(instanceManager, slaveConnectionManager, mediator);

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

    MoveFlightRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, IMaestroConnectionManager? connectionManager = null, IMediator? mediator = null)
    {
        var arrivalLookup = Substitute.For<IArrivalLookup>();
        var trajectoryService = Substitute.For<ITrajectoryService>();
        mediator ??= Substitute.For<IMediator>();
        var clock = clockFixture.Instance;
        return new MoveFlightRequestHandler(
            instanceManager,
            connectionManager ?? new MockLocalConnectionManager(),
            arrivalLookup,
            trajectoryService,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
