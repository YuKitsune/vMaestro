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

public class InsertPendingRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenFlightIsInserted_AndItDoesNotExistInThePendingList_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA999",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act and Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA999 was not found in the pending list.");
    }

    [Fact]
    public async Task WhenFlightIsInserted_ItBecomesStable()
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
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.State.ShouldBe(State.Stable, "flight state should be set to Stable when inserted");
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(targetLandingTime, ["34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.LandingTime.ShouldBe(targetLandingTime, "landing time should be set to target time");
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_TheFlightIsInsertedBeforeTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithFeederFixEstimate(now.AddMinutes(8))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(existingFlight))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
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
            .WithLandingEstimate(now.AddMinutes(20))
            .WithLandingTime(now.AddMinutes(20))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(26))
            .WithLandingTime(now.AddMinutes(26))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var pendingFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
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
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_ButEstimateIsFurtherAhead_TheFlightIsRepositionedAhead()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        // Create a sequence with multiple flights
        var flight1 = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        // Pending flight has estimate way ahead (T-8) of the reference flight (T+13)
        var pendingFlight = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(5))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA789",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // The pending flight should be repositioned to minimize delays
        // Since its estimate (T+25) is way ahead of flight2 (T+13), it should end up after flight3
        sequence.NumberInSequence(pendingFlight).ShouldBe(1, "QFA789 should be first in sequence after repositioning");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA456 should be third in sequence");

        // Times should be optimized - no unnecessary delays
        pendingFlight.LandingTime.ShouldBe(now.AddMinutes(5), "QFA789 should be at its estimate");
        flight1.LandingTime.ShouldBe(now.AddMinutes(10), "QFA123 should remain at its original time");
        flight2.LandingTime.ShouldBe(now.AddMinutes(13), "QFA456 should remain at its original time");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_ButEstimateIsWayBehind_TheFlightIsRepositionedBehind()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        // Create a sequence with multiple flights
        var flight1 = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        // Pending flight has estimate way ahead (T-8) of the reference flight (T+13)
        var pendingFlight = new FlightBuilder("QFA789")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA789",
            new RelativeInsertionOptions("QFA123", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // The pending flight should be repositioned to minimize delays
        // Since its estimate (T+25) is way ahead of flight2 (T+13), it should end up after flight3
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second in sequence");
        sequence.NumberInSequence(pendingFlight).ShouldBe(3, "QFA789 should be third in sequence after repositioning");

        // Times should be optimized - no unnecessary delays
        flight1.LandingTime.ShouldBe(now.AddMinutes(10), "QFA123 should remain at its original time");
        flight2.LandingTime.ShouldBe(now.AddMinutes(13), "QFA456 should remain at its original time");
        pendingFlight.LandingTime.ShouldBe(now.AddMinutes(16), "QFA789 should be delayed behind QFA456");
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
            .WithLandingEstimate(now.AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, instance, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(frozenFlight1, frozenFlight2))
            .Build();

        instance.Session.PendingFlights.Add(pendingFlight);

        // Sanity check - ensure the two frozen flights are correctly positioned
        frozenFlight1.LandingTime.ShouldBe(now.AddMinutes(10));
        frozenFlight2.LandingTime.ShouldBe(now.AddMinutes(13));

        var handler = GetRequestHandler(instanceManager, sequence, clockFixture.Instance);
        var request = new InsertPendingRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("Cannot insert flight", Case.Insensitive);
    }

    InsertPendingRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence, IClock clock)
    {
        var mediator = Substitute.For<IMediator>();
        return new InsertPendingRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            clock,
            mediator,
            Substitute.For<ILogger>());
    }
}
