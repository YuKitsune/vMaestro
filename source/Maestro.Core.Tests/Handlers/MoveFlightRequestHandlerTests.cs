using Maestro.Core.Configuration;
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

public class MoveFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

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
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1S",
            ["34L"],
            frozen.LandingTime.AddMinutes(5));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        moving.LandingTime.ShouldBe(frozen.LandingTime.AddMinutes(5));
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
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["34L"],
            newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingTime.ShouldBe(newTime);
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
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["34L"],
            newTime);

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
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["34L"],
            newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state);
    }

    [Fact]
    public async Task WhenFlightIsMoved_BetweenTwoFrozenFlights_WithNoConflict_FlightIsMovedToRequestedTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozen1 = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(10))
            .Build();
        var frozen2 = new FlightBuilder("QFA2")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(30))
            .Build();
        var subject = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(40))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(frozen1)
            .WithFlight(frozen2)
            .WithFlight(subject)
            .Build();

        var handler = GetRequestHandler(sequence);
        var requestedTime = now.AddMinutes(20);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            requestedTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        subject.LandingTime.ShouldBe(requestedTime);
    }

    [Fact]
    public async Task WhenFlightIsMoved_BetweenTwoFrozenFlights_TooCloseToLeader_FlightIsMovedAfterRequestedTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozen1 = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(10))
            .Build();
        var frozen2 = new FlightBuilder("QFA2")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(20))
            .Build();
        var subject = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(30))
            .Build();


        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(frozen1)
            .WithFlight(frozen2)
            .WithFlight(subject)
            .Build();

        var handler = GetRequestHandler(sequence);
        var requestedTime = now.AddMinutes(12);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            requestedTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        subject.LandingTime.ShouldBe(frozen1.LandingTime.Add(TimeSpan.FromSeconds(180)));
    }

    [Fact]
    public async Task WhenFlightIsMoved_BetweenTwoFrozenFlights_TooCloseToTrailer_FlightIsMovedBeforeRequestedTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozen1 = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(10))
            .Build();
        var frozen2 = new FlightBuilder("QFA2")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(20))
            .Build();
        var subject = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(30))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(frozen1)
            .WithFlight(frozen2)
            .WithFlight(subject)
            .Build();

        var handler = GetRequestHandler(sequence);
        var requestedTime = now.AddMinutes(18);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            requestedTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        subject.LandingTime.ShouldBe(frozen2.LandingTime.AddSeconds(-180));
    }

    [Fact]
    public async Task WhenFlightIsMoved_BetweenTwoFrozenFlights_WithNoSpaceBetweenThem_ExceptionIsThrown()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozen1 = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(10))
            .Build();
        var frozen2 = new FlightBuilder("QFA2")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(15))
            .Build();
        var subject = new FlightBuilder("QFA3")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(frozen1)
            .WithFlight(frozen2)
            .WithFlight(subject)
            .Build();

        var handler = GetRequestHandler(sequence);
        var requestedTime = now.AddMinutes(13);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            requestedTime);

        // Act/Assert
        await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    public async Task WhenFlightIsMoved_BetweenTwoNonFrozenFlights_FlightIsMovedToRequestedTime(State state)
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight1 = new FlightBuilder("QFA1")
            .WithState(state)
            .WithLandingTime(now.AddMinutes(10))
            .Build();
        var flight2 = new FlightBuilder("QFA2")
            .WithState(state)
            .WithLandingTime(now.AddMinutes(15))
            .Build();
        var subject = new FlightBuilder("QFA3")
            .WithState(state)
            .WithLandingTime(now.AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight1)
            .WithFlight(flight2)
            .WithFlight(subject)
            .Build();

        var handler = GetRequestHandler(sequence);
        var requestedTime = now.AddMinutes(12);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA3",
            ["34L"],
            requestedTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        subject.LandingTime.ShouldBe(requestedTime);
    }

    [Fact]
    public async Task WhenFlightIsMoved_WithFeederFixTime_FeederFixTimeIsUpdated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var landingEstimate = now.AddMinutes(20);
        var originalLandingTime = now.AddMinutes(25);

        var feederFixEstimate = now.AddMinutes(10);
        var originalFeederFixTime = now.AddMinutes(15);

        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingEstimate(landingEstimate)
            .WithLandingTime(originalLandingTime)
            .WithFeederFixEstimate(feederFixEstimate)
            .WithFeederFixTime(originalFeederFixTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);

        var newLandingTime = now.AddMinutes(30);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["34L"],
            newLandingTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingTime.ShouldBe(newLandingTime);
        var newFeederFixTime = now.AddMinutes(20);
        flight.FeederFixTime.ShouldBe(newFeederFixTime);
    }

    [Fact]
    public async Task WhenFlightIsMoved_WithNoMatchingRunway_DefaultRunwayIsUsed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA1")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var newTime = now.AddMinutes(20);
        var request = new MoveFlightRequest(
            "YSSY",
            "QFA1",
            ["16R"], // Not in mode
            newTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.LandingTime.ShouldBe(newTime);
        flight.AssignedRunwayIdentifier.ShouldBe("34L"); // Default runway
    }

    MoveFlightRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        var scheduler = Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();
        return new MoveFlightRequestHandler(sessionManager, scheduler, mediator, clockFixture.Instance);
    }
}
