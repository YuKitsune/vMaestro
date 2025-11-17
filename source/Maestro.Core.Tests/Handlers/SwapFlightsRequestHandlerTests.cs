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
        sequence.Insert(0, firstFlight);
        sequence.Insert(1, secondFlight);

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
        sequence.Insert(0, firstFlight);
        sequence.Insert(1, secondFlight);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));
        secondFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(10));
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirFeederFixTimesAreReCalculated()
    {
        // Arrange
        var firstLandingTime = _clock.UtcNow().AddMinutes(10);
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(0))
            .WithFeederFixTime(_clock.UtcNow().AddMinutes(0))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingTime(firstLandingTime)
            .Build();

        var secondLandingTime = _clock.UtcNow().AddMinutes(20);
        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithFeederFixTime(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(secondLandingTime)
            .Build();

        // Arrival intervals here are different to demonstrate that feeder fix times are re-calculated
        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetArrivalInterval(
                Arg.Any<string>(),
                Arg.Is("RIVET"),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftCategory>())
            .Returns(TimeSpan.FromMinutes(10));
        arrivalLookup.GetArrivalInterval(
                Arg.Any<string>(),
                Arg.Is("MARLN"),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftCategory>())
            .Returns(TimeSpan.FromMinutes(15));

        var sequence = new SequenceBuilder(_airportConfiguration).WithArrivalLookup(arrivalLookup).Build();
        sequence.Insert(0, firstFlight);
        sequence.Insert(1, secondFlight);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.FeederFixTime.ShouldBe(secondLandingTime.Subtract(TimeSpan.FromMinutes(10)));
        secondFlight.FeederFixTime.ShouldBe(firstLandingTime.Subtract(TimeSpan.FromMinutes(15)));
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
        sequence.Insert(0, firstFlight);
        sequence.Insert(1, secondFlight);

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
        sequence.Insert(0, firstFlight);
        sequence.Insert(0, secondFlight);

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
        sequence.Insert(0, flight);

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
        sequence.Insert(0, flight);

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
        sequence.Insert(0, firstFlight);
        sequence.Insert(1, secondFlight);

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
        sequence.Insert(0, firstFlight);
        sequence.Insert(1, secondFlight);
        sequence.Insert(2, thirdFlight);

        // Artificial 10-minute delay to ensure recomputation is not performed
        thirdFlight.SetSequenceData(_clock.UtcNow().AddMinutes(40), _clock.UtcNow().AddMinutes(30), FlowControls.ReduceSpeed);

        var handler = GetHandler(sequence);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        thirdFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(40));
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Swap the two flights

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }

    SwapFlightsRequestHandler GetHandler(Sequence sequence)
    {
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
            new MockInstanceManager(sequence),
            new MockLocalConnectionManager(),
            arrivalLookup,
            Substitute.For<MediatR.IMediator>(),
            _clock,
            Substitute.For<ILogger>());
    }
}
