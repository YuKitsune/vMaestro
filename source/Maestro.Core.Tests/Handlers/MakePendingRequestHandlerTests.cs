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

public class MakePendingRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task WhenMakingFlightPending_ItIsRemovedFromTheSequence()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight1 = new FlightBuilder("QFA123")
            .FromDepartureAirport()
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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial state
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.State.ShouldBe(State.Unstable, "flight should be marked as unstable when made pending");

        // Verify flight is moved to pending list and removed from main sequence
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        var sessionMessage = instance.Session.Snapshot();
        sessionMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sessionMessage.Sequence.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
        sessionMessage.Sequence.Flights.ShouldContain(f => f.Callsign == "QFA456", "other flight should remain in sequence");

        // Verify remaining flight moved up in sequence
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should now be first in sequence");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "remaining flight should return to its estimate");
    }

    // TODO: Throw an error

    [Fact]
    public async Task WhenFlightNotFound_WarningIsLoggedAndHandlerReturns()
    {
        // Arrange
        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration).Build();

        var logger = Substitute.For<ILogger>();
        var handler = GetRequestHandler(instanceManager, sequence, logger: logger);
        var request = new MakePendingRequest("YSSY", "NONEXISTENT");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        logger.Received(1).Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", "NONEXISTENT", "YSSY");
    }

    [Fact]
    public async Task WhenFlightIsNotFromDepartureAirport_ExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithRunway("34L")
            .Build();

        // Mark flight as NOT from departure airport
        flight.IsFromDepartureAirport = false;

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("departure airport", Case.Insensitive);
    }

    [Fact]
    public async Task WhenFlightIsMadePending_ItsDetailsAreReset()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .WithRunway("34L")
            .Build();

        // Set some additional properties that should be reset
        flight.SetSequenceData(flight.LandingTime, flight.FeederFixTime, FlowControls.ReduceSpeed);
        flight.IsFromDepartureAirport = true;

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlight(flight))
            .Build();

        var handler = GetRequestHandler(instanceManager, sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - verify all details are reset as specified in the TODO
        flight.ActivatedTime.ShouldBeNull("ActivatedTime should be reset to null");
        flight.FeederFixEstimate.ShouldBeNull("FeederFixEstimate should be reset to null");
        flight.InitialFeederFixEstimate.ShouldBeNull("InitialFeederFixEstimate should be reset to null");
        flight.FeederFixTime.ShouldBeNull("FeederFixTime should be reset to null");
        flight.AssignedRunwayIdentifier.ShouldBeNull("AssignedRunwayIdentifier should be reset to null");
        flight.LandingEstimate.ShouldBe(default(DateTimeOffset), "LandingEstimate should be reset to default");
        flight.LandingTime.ShouldBe(default(DateTimeOffset), "LandingTime should be reset to default");
        flight.FlowControls.ShouldBe(FlowControls.ProfileSpeed, "FlowControls should be reset to ProfileSpeed");
        flight.State.ShouldBe(State.Unstable, "State should be reset to Unstable");
    }

    MakePendingRequestHandler GetRequestHandler(IMaestroInstanceManager instanceManager, Sequence sequence, ILogger? logger = null)
    {
        var mediator = Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();
        return new MakePendingRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            logger);
    }
}
