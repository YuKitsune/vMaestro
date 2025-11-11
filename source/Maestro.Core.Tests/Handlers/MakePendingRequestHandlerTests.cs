using Maestro.Core.Configuration;
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

public class MakePendingRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task WhenMakingFlightPending_AllRequiredUpdatesArePerformed()
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

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Verify initial state
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first initially");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second initially");

        var handler = GetRequestHandler(sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.State.ShouldBe(State.Unstable, "flight should be marked as unstable when made pending");

        // Verify flight is moved to pending list and removed from main sequence
        var sequenceMessage = sequence.ToMessage();
        sequenceMessage.PendingFlights.ShouldContain(f => f.Callsign == "QFA123", "flight should be in pending list");
        sequenceMessage.Flights.ShouldNotContain(f => f.Callsign == "QFA123", "flight should not be in main sequence");
        sequenceMessage.Flights.ShouldContain(f => f.Callsign == "QFA456", "other flight should remain in sequence");

        // Verify remaining flight moved up in sequence
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should now be first in sequence");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "remaining flight should return to its estimate");
    }

    [Fact]
    public async Task WhenFlightNotFound_WarningIsLoggedAndHandlerReturns()
    {
        // Arrange
        var sequence = new SequenceBuilder(_airportConfiguration)
            .Build();

        var logger = Substitute.For<ILogger>();
        var handler = GetRequestHandler(sequence, logger: logger);
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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
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
            .WithRunway("34L", manual: true)
            .WithEstimatedDeparture(now.AddMinutes(2)) // Will depart in 2 minutes
            .Build();

        // Set some additional properties that should be reset
        flight.SetFlowControls(FlowControls.ReduceSpeed);
        flight.IsFromDepartureAirport = true;

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - verify all details are reset as specified in the TODO
        flight.ActivatedTime.ShouldBeNull("ActivatedTime should be reset to null");
        flight.FeederFixEstimate.ShouldBeNull("FeederFixEstimate should be reset to null");
        flight.InitialFeederFixEstimate.ShouldBeNull("InitialFeederFixEstimate should be reset to null");
        flight.FeederFixTime.ShouldBeNull("FeederFixTime should be reset to null");
        flight.AssignedRunwayIdentifier.ShouldBeNull("AssignedRunwayIdentifier should be reset to null");
        flight.RunwayManuallyAssigned.ShouldBeFalse("RunwayManuallyAssigned should be reset to false");
        flight.LandingEstimate.ShouldBe(default(DateTimeOffset), "LandingEstimate should be reset to default");
        flight.LandingTime.ShouldBe(default(DateTimeOffset), "LandingTime should be reset to default");
        flight.FlowControls.ShouldBe(FlowControls.ProfileSpeed, "FlowControls should be reset to ProfileSpeed");
        flight.State.ShouldBe(State.Unstable, "State should be reset to Unstable");
    }

    MakePendingRequestHandler GetRequestHandler(Sequence sequence, ILogger? logger = null)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        var mediator = Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();
        return new MakePendingRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            mediator,
            logger);
    }
}
