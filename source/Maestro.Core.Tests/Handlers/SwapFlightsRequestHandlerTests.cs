using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class SwapFlightsRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;
    readonly FixedClock _clock = clockFixture.Instance;

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirLandingTimesAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.ScheduledLandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));
        secondFlight.ScheduledLandingTime.ShouldBe(_clock.UtcNow().AddMinutes(10));
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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // First flight gets second flight's landing time (20 min) and feeder fix time should be adjusted
        firstFlight.ScheduledFeederFixTime.ShouldBe(_clock.UtcNow().AddMinutes(10));

        // Second flight gets first flight's landing time (10 min) and feeder fix time should be adjusted
        secondFlight.ScheduledFeederFixTime.ShouldBe(_clock.UtcNow().AddMinutes(0));
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirRunwaysAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

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
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

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
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(
            () => handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("Could not find QFA1");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndSecondFlightDoesNotExist_AnErrorIsThrown()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingTime(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(
            () => handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("Could not find QFA2");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndTheyAreNotUnstable_UpdateStateBasedOnTimeIsCalled()
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

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(firstFlight)
            .WithFlight(secondFlight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);
        var handler = new SwapFlightsRequestHandler(sequenceProvider, Substitute.For<MediatR.IMediator>(), _clock);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.State.ShouldBe(State.SuperStable);
        secondFlight.State.ShouldBe(State.Frozen);
    }
}
