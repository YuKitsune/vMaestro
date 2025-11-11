using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class SwapFlightsRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;
    readonly FixedClock _clock = clockFixture.Instance;

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirPositionsAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);

        // Sanity check
        sequence.NumberForRunway(firstFlight).ShouldBe(1);
        sequence.NumberInSequence(firstFlight).ShouldBe(1);
        sequence.NumberForRunway(secondFlight).ShouldBe(1);
        sequence.NumberInSequence(secondFlight).ShouldBe(2);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberForRunway(firstFlight).ShouldBe(1);
        sequence.NumberInSequence(firstFlight).ShouldBe(2);
        sequence.NumberForRunway(secondFlight).ShouldBe(1);
        sequence.NumberInSequence(secondFlight).ShouldBe(1);
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirLandingTimesAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));
        secondFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(10));
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirFeederFixTimesAreUpdated()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(0))
            .WithFeederFixTime(_clock.UtcNow().AddMinutes(0))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithFeederFixTime(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // First flight gets second flight's landing time (20 min) and feeder fix time should be adjusted
        firstFlight.FeederFixTime.ShouldBe(_clock.UtcNow().AddMinutes(10));

        // Second flight gets first flight's landing time (10 min) and feeder fix time should be adjusted
        secondFlight.FeederFixTime.ShouldBe(_clock.UtcNow().AddMinutes(0));
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirRunwaysAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        secondFlight.AssignedRunwayIdentifier.ShouldBe("34L");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndTheyAreUnstable_TheyBecomeStable()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        firstFlight.State.ShouldBe(State.Stable);
        secondFlight.State.ShouldBe(State.Stable);
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndFirstFlightDoesNotExist_AnErrorIsThrown()
    {
        // Arrange
        var flight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(
            () => handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA1 not found");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndSecondFlightDoesNotExist_AnErrorIsThrown()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(flight, flight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(
            () => handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA2 not found");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndTheyAreNotUnstable_TheirStateRemainsUnchanged()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithFeederFixEstimate(_clock.UtcNow())
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(5))
            .WithLandingTime(_clock.UtcNow().AddMinutes(5))
            .WithState(State.Frozen)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithState(State.SuperStable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.State.ShouldBe(State.Frozen);
        secondFlight.State.ShouldBe(State.SuperStable);
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheSequenceIsNotRecomputed()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var thirdFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(30))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.Insert(firstFlight, firstFlight.LandingEstimate);
        sequence.Insert(secondFlight, secondFlight.LandingEstimate);
        sequence.Insert(thirdFlight, thirdFlight.LandingEstimate);

        thirdFlight.SetLandingTime(_clock.UtcNow().AddMinutes(40)); // Artificial 10-minute delay to ensure recomputation is not performed

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        thirdFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(40));
    }

    SwapFlightsRequestHandler GetHandler(Sequence sequence)
    {
        var sessionManager = new MockLocalSessionManager(sequence);

        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetArrivalInterval(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftCategory>())
            .Returns(TimeSpan.FromMinutes(10));

        return new SwapFlightsRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            arrivalLookup,
            Substitute.For<MediatR.IMediator>(),
            _clock,
            Substitute.For<ILogger>());
    }
}
