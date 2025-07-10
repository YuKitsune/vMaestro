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

public class MoveFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task WhenFlightIsMoved_BetweenFrozenFlights_ExceptionIsThrown()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var frozen1 = new FlightBuilder("QFA1F").WithState(State.Frozen).WithLandingTime(now.AddMinutes(10)).Build();
        var subject = new FlightBuilder("QFA1S").WithState(State.Stable).WithLandingTime(now.AddMinutes(20)).Build();
        var frozen2 = new FlightBuilder("QFA2F").WithState(State.Frozen).WithLandingTime(now.AddMinutes(30)).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(frozen1);
        sequence.Add(subject);
        sequence.Add(frozen2);

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1S", frozen1.ScheduledLandingTime.AddMinutes(-1));

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task WhenFlightIsMoved_LandingTimeIsSet()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var flight = new FlightBuilder("QFA1").WithState(State.Stable).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var newTime = now.AddMinutes(20);
        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ScheduledLandingTime.ShouldBe(newTime);
    }

    [Fact]
    public async Task WhenUnstableFlightIsMoved_ItBecomesStable()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var flight = new FlightBuilder("QFA1").WithState(State.Unstable).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

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
    public async Task WhenFlightIsMoved_StateIsUnchanged(State state)
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var flight = new FlightBuilder("QFA1").WithState(state).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

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
        var now = DateTimeOffset.UtcNow;
        var flight = new FlightBuilder("QFA1").WithState(state).WithLandingTime(now.AddMinutes(10)).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var handler = GetRequestHandler(sequence);
        var newTime = now.AddMinutes(20);
        var request = new MoveFlightRequest("YSSY", "QFA1", newTime);

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    // These are more testing the scheduler and should probably be moved to the scheduler tests.

    [Fact]
    public async Task WhenFlightIsMoved_AndConflictsWithTrafficAhead_LeaderIsDelayedBehindTheMovedFlight()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var flight1 = new FlightBuilder("QFA1").WithState(State.Stable).WithLandingTime(now.AddMinutes(10)).WithRunway("34L").Build();
        var flight2 = new FlightBuilder("QFA2").WithState(State.Stable).WithLandingTime(now.AddMinutes(15)).WithRunway("34L").Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight1);
        sequence.Add(flight2);

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", flight2.ScheduledLandingTime.AddMinutes(-2));
        sequence.ChangeRunwayMode(new RunwayMode
        {
            Identifier = "34",
            Runways = [new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = 180 }]
        });

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.NeedsRecompute.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenFlightIsMoved_AndConflictsWithTrafficBehind_TrailerIsDelayed()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var flight1 = new FlightBuilder("QFA1").WithState(State.Stable).WithLandingTime(now.AddMinutes(10)).WithRunway("34L").Build();
        var flight2 = new FlightBuilder("QFA2").WithState(State.Stable).WithLandingTime(now.AddMinutes(8)).WithRunway("34L").Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight2);
        sequence.Add(flight1);

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", flight2.ScheduledLandingTime.AddMinutes(2));
        sequence.ChangeRunwayMode(new RunwayMode
        {
            Identifier = "34",
            Runways = [new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = 180 }]
        });

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.NeedsRecompute.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenFlightIsMovedToSameTime_NoChangeOccurs()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var flight = new FlightBuilder("QFA1").WithState(State.Stable).WithLandingTime(now).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(flight);

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1", now);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ScheduledLandingTime.ShouldBe(now);
    }

    [Fact]
    public async Task WhenFlightIsMoved_AfterFrozenFlight_Succeeds()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var frozen = new FlightBuilder("QFA1F").WithState(State.Frozen).WithLandingTime(now.AddMinutes(10)).Build();
        var moving = new FlightBuilder("QFA1S").WithState(State.Stable).WithLandingTime(now.AddMinutes(20)).Build();

        var sequence = new Sequence(_airportConfiguration);
        sequence.Add(frozen);
        sequence.Add(moving);

        var handler = GetRequestHandler(sequence);
        var request = new MoveFlightRequest("YSSY", "QFA1S", frozen.ScheduledLandingTime.AddMinutes(5));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        moving.ScheduledLandingTime.ShouldBe(frozen.ScheduledLandingTime.AddMinutes(5));
    }

    MoveFlightRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.CanSequenceFor(Arg.Is("YSSY")).Returns(true);
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));

        var mediator = Substitute.For<IMediator>();
        return new MoveFlightRequestHandler(sequenceProvider, mediator);
    }
}
