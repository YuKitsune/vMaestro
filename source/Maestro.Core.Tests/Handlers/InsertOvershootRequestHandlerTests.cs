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

public class InsertOvershootRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenFlightIsInserted_TheStateIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.State.ShouldBe(State.Frozen, "flight state should be set to Frozen when inserted as overshoot");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndItDoesNotExistInTheOvershootList_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA999",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("Flight QFA999 not found in landed flights");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_ThePositionInTheSequenceIsUpdated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);
        sequence.Insert(1, existingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(landedFlight).ShouldBeLessThan(sequence.NumberInSequence(existingFlight),
            "QFA123 should be positioned before QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_TheLandingTimeAndRunwayAreUpdated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetLandingTime = now.AddMinutes(20);

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(targetLandingTime, ["34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.LandingTime.ShouldBe(targetLandingTime, "landing time should be set to target time");
        landedFlight.AssignedRunwayIdentifier.ShouldBe("34R", "runway should be set to 34R");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_ThePositionInTheSequenceIsUpdated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);
        sequence.Insert(1, existingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(landedFlight).ShouldBeLessThan(sequence.NumberInSequence(existingFlight),
            "QFA123 should be positioned before QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_TheFlightIsInsertedBeforeTheReferenceFlightAndTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);
        sequence.Insert(1, flight1);
        sequence.Insert(2, flight2);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(landedFlight).ShouldBeLessThan(sequence.NumberInSequence(flight1),
            "QFA123 should be positioned before QFA456");
        sequence.NumberInSequence(flight1).ShouldBeLessThan(sequence.NumberInSequence(flight2),
            "QFA456 should be positioned before QFA789");

        // The reference flight and trailing flights should be delayed
        flight1.LandingTime.ShouldBe(landedFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA456 should be delayed to maintain separation behind QFA123");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_ThePositionInTheSequenceIsUpdated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);
        sequence.Insert(1, existingFlight);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(existingFlight).ShouldBeLessThan(sequence.NumberInSequence(landedFlight),
            "QFA456 should be positioned before QFA123");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_TheFlightIsInsertedBehindTheReferenceFlightAndAnyTrailingConflictsAreDelayed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);
        sequence.Insert(1, flight1);
        sequence.Insert(2, flight2);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBeLessThan(sequence.NumberInSequence(landedFlight),
            "QFA456 should be positioned before QFA123");
        sequence.NumberInSequence(landedFlight).ShouldBeLessThan(sequence.NumberInSequence(flight2),
            "QFA123 should be positioned before QFA789");

        // The inserted flight should be positioned behind the reference flight with proper separation
        landedFlight.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA123 should be scheduled with separation behind QFA456 (the reference flight)");

        // Trailing flight should be delayed further
        flight2.LandingTime.ShouldBe(landedFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA123");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BetweenTwoFrozenFlights_WithoutEnoughSpaceBetweenThem_AnExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, landedFlight);
        sequence.Insert(1, frozenFlight1);
        sequence.Insert(2, frozenFlight2);

        var handler = GetRequestHandler(sequence, clockFixture.Instance);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("Cannot insert flight", Case.Insensitive);
    }

    InsertOvershootRequestHandler GetRequestHandler(Sequence sequence, IClock clock, IMaestroInstanceManager? instanceManager = null)
    {
        instanceManager ??= new MockInstanceManager(sequence);
        var mediator = Substitute.For<IMediator>();
        return new InsertOvershootRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            clock,
            mediator,
            Substitute.For<ILogger>());
    }
}
