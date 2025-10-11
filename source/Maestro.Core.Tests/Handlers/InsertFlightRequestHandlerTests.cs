using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class InsertFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenFlightIsInserted_TheStateIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");
        dummyFlight.State.ShouldBe(State.Frozen, "dummy flight state should be set to Frozen when inserted");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndCallsignIsProvided_TheProvidedCallsignIsUsed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "MYCALL123",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single();
        dummyFlight.Callsign.ShouldBe("MYCALL123", "provided callsign should be used");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndInvalidCallsignIsProvided_ItIsMadeUppercaseAndTruncated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "lowercase_very_long_callsign",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single();
        dummyFlight.Callsign.ShouldBe("LOWERCASE_VE", "callsign should be uppercase and truncated to 12 characters");
        dummyFlight.Callsign.Length.ShouldBeLessThanOrEqualTo(12, "callsign should not exceed 12 characters");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AndAircraftTypeIsProvided_TheProvidedAircraftTypeIsUsed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "A388",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");
        dummyFlight.AircraftType.ShouldBe("A388", "provided aircraft type should be used");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_ThePositionInTheSequenceIsUpdated()
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(existingFlight, existingFlight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new ExactInsertionOptions(now.AddMinutes(20), ["34L"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");

        // Verify the dummy flight is first in the sequence
        sequence.NumberInSequence(dummyFlight).ShouldBeLessThan(sequence.NumberInSequence(existingFlight),
            "TEST123 should be positioned before QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_WithExactInsertionOptions_TheLandingTimeAndRunwayAreUpdated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetLandingTime = now.AddMinutes(20);

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new ExactInsertionOptions(targetLandingTime, ["34R"]));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");
        dummyFlight.LandingTime.ShouldBe(targetLandingTime, "landing time should be set to target time");

        // TODO: Assert that the runway is assigned to something in mode
        dummyFlight.AssignedRunwayIdentifier.ShouldBe("34R", "runway should be set to 34R");
    }

    [Fact]
    public async Task WhenFlightIsInserted_BeforeAnotherFlight_ThePositionInTheSequenceIsUpdated()
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(existingFlight, existingFlight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");

        // Verify the dummy flight is first in the sequence
        sequence.NumberInSequence(dummyFlight).ShouldBeLessThan(sequence.NumberInSequence(existingFlight),
            "TEST123 should be positioned before QFA456");
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new RelativeInsertionOptions("QFA456", RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");

        // The reference flight and trailing flights should be delayed
        flight1.LandingTime.ShouldBe(dummyFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA456 should be delayed to maintain separation behind TEST123");
        flight2.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind QFA456");
    }

    [Fact]
    public async Task WhenFlightIsInserted_AfterAnotherFlight_ThePositionInTheSequenceIsUpdated()
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(existingFlight, existingFlight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");

        // Verify the dummy flight is after the existing flight
        sequence.NumberInSequence(dummyFlight).ShouldBeGreaterThan(sequence.NumberInSequence(existingFlight),
            "TEST123 should be positioned after QFA456");
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var dummyFlight = sequence.DummyFlights.Single(f => f.Callsign == "TEST123");

        // The inserted dummy flight should be positioned behind the reference flight with proper separation
        dummyFlight.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "TEST123 should be scheduled with separation behind QFA456 (the reference flight)");

        // Trailing flight should be delayed further
        flight2.LandingTime.ShouldBe(dummyFlight.LandingTime.Add(airportConfigurationFixture.AcceptanceRate),
            "QFA789 should be delayed to maintain separation behind TEST123");
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithClock(clockFixture.Instance)
            .Build();
        sequence.Insert(frozenFlight1, frozenFlight1.LandingEstimate);
        sequence.Insert(frozenFlight2, frozenFlight2.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "TEST123",
            "B738",
            new RelativeInsertionOptions("QFA456", RelativePosition.After));

        // Act & Assert
        // TODO: This should throw an exception when the validation is implemented
        // For now, this test documents the expected behavior but will fail until implemented
        var exception = await Should.ThrowAsync<MaestroException>(async () =>
            await handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldContain("frozen", Case.Insensitive);
    }

    InsertFlightRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        var mediator = Substitute.For<IMediator>();
        return new InsertFlightRequestHandler(sessionManager, mediator);
    }
}
