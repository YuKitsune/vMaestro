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

        sequence.Insert(0, flight1);
        sequence.Insert(1, flight2);

        // Verify initial state
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");

        var instanceManager = new MockInstanceManager(sequence);

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        var sessionMessage = instance.Session.Snapshot();

        sessionMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sessionMessage.Sequence.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
        sessionMessage.Sequence.Flights.ShouldContain(f => f.Callsign == "QFA456", "other flight should remain in sequence");

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

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.DeSequencedFlights.Add(flight1);

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var sessionMessage = instance.Session.Snapshot();
        sessionMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sessionMessage.DeSequencedFlights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in desequenced list");
        sessionMessage.Sequence.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
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
        flight.SetSequenceData(
            clockFixture.Instance.UtcNow().AddMinutes(30),
            clockFixture.Instance.UtcNow().AddMinutes(20),
            FlowControls.ReduceSpeed);

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(0, flight);

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

        var instanceManager = new MockInstanceManager(sequence);
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.PendingFlights.Add(flight);

        var handler = GetRequestHandler(sequence);
        var request = new RemoveRequest("YSSY", "QFA123");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("not found", Case.Insensitive);
    }

    RemoveRequestHandler GetRequestHandler(Sequence sequence, IMaestroInstanceManager? instanceManager = null)
    {
        instanceManager ??= new MockInstanceManager(sequence);
        var mediator = Substitute.For<IMediator>();

        return new RemoveRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());
    }
}
