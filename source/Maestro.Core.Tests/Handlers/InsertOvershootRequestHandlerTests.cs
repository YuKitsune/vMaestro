using Maestro.Core.Connectivity;
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
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
        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        var handler = GetRequestHandler(instanceManager);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(landedFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(landedFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(existingFlight).ShouldBe(2, "QFA456 should be second in sequence");
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(landedFlight).ShouldBe(1, "QFA123 should be first in sequence");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA456 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");

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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(existingFlight).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(landedFlight).ShouldBe(2, "QFA123 should be second in sequence");
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, flight1, flight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA456 should be first in sequence");
        sequence.NumberInSequence(landedFlight).ShouldBe(2, "QFA123 should be second in sequence");
        sequence.NumberInSequence(flight2).ShouldBe(3, "QFA789 should be third in sequence");

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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, frozenFlight1, frozenFlight2))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("Cannot insert flight");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_LandingEstimateIsSetToProvidedTime()
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(targetLandingTime, ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.LandingEstimate.ShouldBe(targetLandingTime,
            "landing estimate should be set to the provided time");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_RunwayIsSetBasedOnRunwayModeAtProvidedTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetLandingTime = now.AddMinutes(35);

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        // Change runway mode to 16IVA starting at T+30 (before the target landing time at T+35)
        sequence.ChangeRunwayMode(
            new RunwayMode(new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            })),
            now.AddMinutes(25),
            now.AddMinutes(30));

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(targetLandingTime, ["16L", "34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.AssignedRunwayIdentifier.ShouldBe("16L",
            "runway should be assigned from the 16IVA mode at the target landing time");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BehindAnotherFlight_RunwayIsSetToReferenceFlightRunway()
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
            .WithFeederFix("BOREE")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithFeederFixEstimate(now.AddMinutes(-2))
            .WithState(State.Stable)
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.AssignedRunwayIdentifier.ShouldBe("34R",
            "runway should be set to the reference flight's runway");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AheadOfAnotherFlight_RunwayIsSetToReferenceFlightRunway()
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
            .WithFeederFix("BOREE")
            .WithLandingEstimate(now.AddMinutes(25))
            .WithLandingTime(now.AddMinutes(25))
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.AssignedRunwayIdentifier.ShouldBe("34R",
            "runway should be set to the reference flight's runway");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AheadOfAnotherFlight_LandingTimeIsSetToReferenceFlightLandingTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var referenceLandingTime = now.AddMinutes(25);

        var landedFlight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now)
            .WithLandingTime(now)
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithState(State.Landed)
            .WithRunway("34L")
            .Build();

        var existingFlight = new FlightBuilder("QFA456")
            .WithLandingEstimate(referenceLandingTime)
            .WithLandingTime(referenceLandingTime)
            .WithFeederFixEstimate(now.AddMinutes(13))
            .WithState(State.Stable)
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(landedFlight, existingFlight))
            .Build();

        var handler = GetRequestHandler(instanceManager);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.LandingTime.ShouldBe(referenceLandingTime,
            "landing time should be set to the reference flight's landing time");

        // The reference flight should be delayed to maintain separation
        existingFlight.LandingTime.ShouldBe(referenceLandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "reference flight should be delayed to maintain separation");
    }

    [Fact]
    public async Task RedirectedToMaster()
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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(landedFlight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = GetRequestHandler(instanceManager, slaveConnectionManager, mediator);

        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        landedFlight.LandingTime.ShouldBe(now, "Flight landing time should not change when relaying to master");
    }

    InsertOvershootRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, IMaestroConnectionManager? connectionManager = null, IMediator? mediator = null)
    {
        var arrivalLookup = Substitute.For<IArrivalLookup>();
        mediator ??= Substitute.For<IMediator>();
        return new InsertOvershootRequestHandler(
            instanceManager,
            connectionManager ?? new MockLocalConnectionManager(),
            arrivalLookup,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());
    }
}
