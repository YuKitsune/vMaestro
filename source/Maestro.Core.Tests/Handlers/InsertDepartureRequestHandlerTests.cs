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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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
    public async Task WhenFlightIsInserted_AndItDoesNotExistInThePendingList_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var instanceManager = new MockInstanceManager(sequence);
        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, existingFlight);

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, existingFlight);

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, existingFlight);

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, frozenFlight1);
        sequence.Insert(1, frozenFlight2);

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance, instanceManager);
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

    InsertDepartureRequestHandler GetRequestHandler(Sequence sequence, IClock clock, IMaestroInstanceManager? instanceManager = null)
    {
        instanceManager ??= new MockInstanceManager(sequence);
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

