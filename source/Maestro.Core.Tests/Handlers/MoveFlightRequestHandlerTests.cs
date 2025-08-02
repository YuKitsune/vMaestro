using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class MoveFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task WhenFlightIsMoved_BetweenFrozenFlights_ExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozen1 = new FlightBuilder("QFA1F").WithState(State.Frozen).WithLandingTime(now.AddMinutes(10)).Build();
        var frozen2 = new FlightBuilder("QFA2F").WithState(State.Frozen).WithLandingTime(now.AddMinutes(20)).Build();
        var subject = new FlightBuilder("QFA1S").WithState(State.Stable).WithLandingTime(now.AddMinutes(30)).Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(frozen1)
            .WithFlight(frozen2)
            .WithFlight(subject)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1S", frozen1.ScheduledLandingTime.AddMinutes(-1));

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task WhenFlightIsMoved_AfterFrozenFlight_Succeeds()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozen = new FlightBuilder("QFA1F").WithState(State.Frozen).WithLandingTime(now.AddMinutes(10)).Build();
        var moving = new FlightBuilder("QFA1S").WithState(State.Stable).WithLandingTime(now.AddMinutes(20)).Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(frozen)
            .WithFlight(moving)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1S", frozen.ScheduledLandingTime.AddMinutes(5));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        moving.ScheduledLandingTime.ShouldBe(frozen.ScheduledLandingTime.AddMinutes(5));
    }

    [Fact]
    public async Task WhenFlightIsMoved_LandingTimeIsSet()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(State.Stable).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var newTime = now.AddMinutes(20);
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ScheduledLandingTime.ShouldBe(newTime);
        flight.ManualLandingTime.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenUnstableFlightIsMoved_ItBecomesStable()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(State.Unstable).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var newTime = now.AddMinutes(20);
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable);
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public async Task WhenStableFlightIsMoved_StateIsUnchanged(State state)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(state).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var newTime = now.AddMinutes(20);
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state);
    }

    [Theory]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    [InlineData(State.Desequenced)]
    [InlineData(State.Removed)]
    public async Task WhenInvalidFlightIsMoved_ExceptionIsThrown(State state)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1").WithState(state).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var newTime = now.AddMinutes(20);
        var request = new MoveFlightRequest("YSSY", "QFA1", newTime);

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    MoveFlightRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sequenceProvider = new MockSequenceProvider(sequence);
        var scheduler = Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();
        return new MoveFlightRequestHandler(sequenceProvider, scheduler, mediator);
    }
}
