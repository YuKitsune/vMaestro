using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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

public class MoveFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    readonly IClock _clock = new FixedClock(new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero));
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task WhenFlightIsMoved_BetweenFrozenFlights_ExceptionIsThrown()
    {
        // Arrange
        var now = _clock.UtcNow();
        var frozen1 = new FlightBuilder("QFA1F").WithState(State.Frozen).Build();
        var frozen2 = new FlightBuilder("QFA2F").WithState(State.Frozen).Build();
        var subject = new FlightBuilder("QFA1S").WithState(State.Stable).Build();

        var sequence = new SlotBasedSequence(_airportConfiguration, _airportConfiguration.RunwayModes.First(), now);
        sequence.Slots[0].AllocateTo(frozen1);
        sequence.Slots[1].AllocateTo(frozen2);
        sequence.Slots[2].AllocateTo(subject);

        var handler = GetRequestHandler(sequence);
        var targetSlot = sequence.Slots[1];
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1S",
            targetSlot.Identifier,
            targetSlot.RunwayIdentifier);

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task WhenFlightIsMoved_LandingTimeIsSet()
    {
        // Arrange
        var now = _clock.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(State.Stable).Build();

        var sequence = new SlotBasedSequence(_airportConfiguration, _airportConfiguration.RunwayModes.First(), now);
        sequence.Slots[0].AllocateTo(flight);

        var newSlot = sequence.Slots[1];
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            newSlot.Identifier,
            newSlot.RunwayIdentifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ScheduledLandingTime.ShouldBe(newSlot.Time);
    }

    [Fact]
    public async Task WhenUnstableFlightIsMoved_ItBecomesStable()
    {
        // Arrange
        var now = _clock.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(State.Unstable).Build();

        var sequence = new SlotBasedSequence(_airportConfiguration, _airportConfiguration.RunwayModes.First(), now);
        sequence.Slots[0].AllocateTo(flight);

        var newSlot = sequence.Slots[1];
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            newSlot.Identifier,
            newSlot.RunwayIdentifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public async Task WhenFlightIsMoved_StateIsUnchanged(State state)
    {
        // Arrange
        var now = _clock.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(state).Build();

        var sequence = new SlotBasedSequence(_airportConfiguration, _airportConfiguration.RunwayModes.First(), now);
        sequence.Slots[0].AllocateTo(flight);

        var newSlot = sequence.Slots[1];
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            newSlot.Identifier,
            newSlot.RunwayIdentifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state);
    }

    [Theory]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task WhenInvalidFlightIsMoved_ExceptionIsThrown(State state)
    {
        // Arrange
        var now = _clock.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(state).Build();

        var sequence = new SlotBasedSequence(_airportConfiguration, _airportConfiguration.RunwayModes.First(), now);
        sequence.Slots[0].AllocateTo(flight);

        var newSlot = sequence.Slots[1];
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            newSlot.Identifier,
            newSlot.RunwayIdentifier);

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task WhenAFlightIsMoved_ToAnOccupiedSlot_OtherFlightsAreDelayed()
    {
        // Arrange
        var now = _clock.UtcNow();
        var flight1 = new FlightBuilder("QFA1").Build();
        var flight2 = new FlightBuilder("QFA2").Build();
        var flight3 = new FlightBuilder("QFA3").NoDelay().Build();
        var flight4 = new FlightBuilder("QFA4").Build();
        var flight5 = new FlightBuilder("QFA5").Build();

        var sequence = new SlotBasedSequence(_airportConfiguration, _airportConfiguration.RunwayModes.First(), now);
        sequence.Slots[0].AllocateTo(flight1);
        sequence.Slots[1].AllocateTo(flight2);
        sequence.Slots[2].AllocateTo(flight3);
        sequence.Slots[3].AllocateTo(flight4);
        // Skipping slot 4 to ensure QFA5 is not delayed any further
        sequence.Slots[5].AllocateTo(flight5);

        // Move QFA4 to the front of the queue
        var targetSlot = sequence.Slots[0];
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA4",
            targetSlot.Identifier,
            targetSlot.RunwayIdentifier);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Slots
            .Take(6)
            .Select(f => f.Flight?.Callsign)
            .ShouldBe([
                "QFA4", // QFA4 is now in front
                "QFA1", // QFA2 is delayed to the slot behind them
                "QFA3", // QFA3 is not delayed because it has NoDelay set
                "QFA2", // QFA2 is put behind QFA3 as QFA3 has NoDelay
                null, // slot 4 remains empty
                "QFA5" // QFA5 remains in place
            ]);
    }

    MoveFlightRequestHandler GetRequestHandler(SlotBasedSequence sequence)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.CanSequenceFor(Arg.Is("YSSY")).Returns(true);
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));

        var mediator = Substitute.For<IMediator>();
        return new MoveFlightRequestHandler(sequenceProvider, mediator, Substitute.For<ILogger>());
    }
}
