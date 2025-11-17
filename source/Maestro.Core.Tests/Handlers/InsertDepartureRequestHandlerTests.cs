using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
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

public class InsertDepartureRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly TimeSpan _arrivalInterval = TimeSpan.FromMinutes(16);

    [Fact]
    public async Task WhenFlightIsInserted_TheStateIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.State.ShouldBe(State.Stable, "flight state should be set to Stable when inserted");
    }

    [Fact]
    public async Task LandingEstimateIsDerivedFromTakeOffTime()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight with a specific estimated enroute time
        // TODO: Add the flight to the pending list

        // Act
        // TODO: Insert the flight with a specific take-off time

        // Assert
        // TODO: Assert that the landing estimate is correctly calculated based on the take-off time and enroute time
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_ItIsInsertedBasedOnItsCalculatedLandingEstimate()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight with a specific estimated enroute time
        // TODO: Add the flight to the pending list
        // TODO: Create two flights in the sequence with landing estimates before and after the calculated landing estimate of the inserted flight

        // Act
        // TODO: Insert the flight with a specific take-off time

        // Assert
        // TODO: Assert that the flight is inserted in the correct position based on its calculated landing estimate
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_RunwayIsAssignedByFeederPreference()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Add a flight to the pending list

        // Act
        // TODO: Insert the flight with a specific take-off time

        // Assert
        // TODO: Assert that the runway is assigned based on the feeder preference
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_RunwayIsAssignedByModeAtLandingEstimate()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Add a flight to the pending list
        // TODO: Change the runway mode before the landing estimate (from 34R to 16L, 5 minutes before landing estimate)

        // Act
        // TODO: Insert the flight with a specific take-off time

        // Assert
        // TODO: Assert that the runway is assigned based on the mode at the calculated landing estimate
    }

    [Fact]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_AndTheLandingEstimateIsBeforeAStableFlight_StableFlightIsDelayed()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Add a flight to the pending list
        // TODO: Add two stable flights to the sequence landing before and after the calculated landing estimate of the inserted flight

        // Act
        // TODO: Insert the flight with a specific take-off time

        // Assert
        // TODO: Assert the landing order is correct (Stabled flight, Inserted flight, Stabled flight)
        // TODO: Assert that the first stable flight is unaffected
        // TODO: Assert that the second stable flight is delayed to maintain separation with the inserted flight
    }

    [Theory]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAFlightIsInserted_WithATakeOffTime_AndTheLandingEstimateIsBeforeASuperStableFlight_InsertedFlightIsDelayed(State state)
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Add a flight to the pending list
        // TODO: Add a flight with the provided state to the sequence landing after the calculated landing estimate of the inserted flight

        // Act
        // TODO: Insert the flight with a specific take-off time

        // Assert
        // TODO: Assert the landing order is correct (Existing flight, Inserted flight)
        // TODO: Assert that the existing flight is unaffected
        // TODO: Assert that the inserted flight is delayed to maintain separation with the existing flight
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndItDoesNotExistInThePendingList_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA999",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act and Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA999 was not found in the pending list.");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndItIsNotFromDepartureAirport_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var pendingFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act and Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA123 is not from a departure airport.");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_ThePositionInTheSequenceIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_TheLandingTimeAndRunwayAreSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetLandingTime = now.AddMinutes(20);

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new ExactInsertionOptions(targetLandingTime, ["34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingTime.ShouldBe(targetLandingTime, "landing time should be set to target time");

        // TODO: Assert that the runway is assigned to something in mode
        pendingFlight.AssignedRunwayIdentifier.ShouldBe("34R", "runway should be set to 34R");
        pendingFlight.RunwayManuallyAssigned.ShouldBe(true, "runway should be marked as manually assigned");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_ThePositionInTheSequenceIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_TheFlightIsInsertedBeforeTheReferenceFlightAndTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithFeederFixEstimate(now.AddMinutes(1))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA456 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");

        // The reference flight and trailing flights should be delayed
        flight1.LandingTime.ShouldBe(pendingFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA456 should be delayed to maintain separation behind QFA123");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_ThePositionInTheSequenceIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(existingFlight).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_TheFlightIsInsertedBehindTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var flight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(13))
            .WithFeederFixEstimate(now.AddMinutes(-1))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(2, "QFA123 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");

        // The inserted flight should be positioned behind the reference flight with proper separation
        pendingFlight.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA123 should be scheduled with separation behind QFA456 (the reference flight)");

        // Trailing flight should be delayed further
        flight2.LandingTime.ShouldBe(pendingFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA123");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BetweenTwoFrozenFlights_WithoutEnoughSpaceBetweenThem_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        // Two frozen flights with only 1x acceptance rate between them (need 2x)
        var frozenFlight1 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var frozenFlight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithFeederFixEstimate(now.AddMinutes(1))
            .WithState(State.Frozen)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .FromDepartureAirport()
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(frozenFlight1, frozenFlight2))
            .Build();
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA123",
            "B738",
            "YSCB",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("Cannot insert flight", Case.Insensitive);
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance
        // TODO: Add a flight to the pending list

        // Act
        // TODO: Insert the flight

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }

    InsertDepartureRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence, IClock clock)
    {
        var mediator = Substitute.For<IMediator>();
        return new InsertDepartureRequestHandler(
            Substitute.For<IAirportConfigurationProvider>(),
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IArrivalLookup>(),
            clock,
            mediator,
            Substitute.For<ILogger>());
    }
}
