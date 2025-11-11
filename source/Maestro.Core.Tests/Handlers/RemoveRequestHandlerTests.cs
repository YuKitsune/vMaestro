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

public class RemoveRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenAnActiveFlightIsRemoved_ItIsPlacedInThePendingList()
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

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(15))
            .WithLandingEstimate(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Verify initial state
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "QFA456", "other flight should remain in sequence");

        // Verify remaining flight moved up in sequence
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should now be first in sequence");
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight1, flight1.LandingEstimate);

        // Desequence the flight first
        sequence.Desequence("QFA123");

        // Verify flight is desequenced
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.DeSequencedFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in desequenced list");

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sequenceMessage.DeSequencedFlights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in desequenced list");
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
    }

    [Fact]
    public async Task WhenAFlightIsRemoved_ItsDetailsAreReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithRunway("34L", manual: true)
            .Build();

        // Set some additional properties that should be reset
        flight.SetFlowControls(FlowControls.ReduceSpeed);

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - verify all details are reset
        flight.ActivatedTime.ShouldBeNull("ActivatedTime should be reset to null");
        flight.FeederFixEstimate.ShouldBeNull("FeederFixEstimate should be reset to null");
        flight.InitialFeederFixEstimate.ShouldBeNull("InitialFeederFixEstimate should be reset to null");
        flight.FeederFixTime.ShouldBeNull("FeederFixTime should be reset to null");
        flight.AssignedRunwayIdentifier.ShouldBeNull("AssignedRunwayIdentifier should be reset to null");
        flight.RunwayManuallyAssigned.ShouldBeFalse("RunwayManuallyAssigned should be reset to false");
        flight.LandingEstimate.ShouldBe(default(DateTimeOffset), "LandingEstimate should be reset to default");
        flight.InitialLandingEstimate.ShouldBe(default(DateTimeOffset), "InitialLandingEstimate should be reset to default");
        flight.LandingTime.ShouldBe(default(DateTimeOffset), "LandingTime should be reset to default");
        flight.FlowControls.ShouldBe(FlowControls.ProfileSpeed, "FlowControls should be reset to ProfileSpeed");
        flight.State.ShouldBe(State.Unstable, "State should be reset to Unstable");
    }

    [Fact]
    public async Task WhenRemovingAFlightThatDoesNotExist_AnExceptionIsThrown()
    {
        // Arrange
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence);
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
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA123")
            .WithLandingEstimate(now.AddMinutes(8))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        // Add flight to pending list
        sequence.AddPendingFlight(flight);

        // Verify flight is in pending list
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("not found", Case.Insensitive);
    }

    RemoveRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        var mediator = Substitute.For<IMediator>();
        return new RemoveRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());
    }
}
