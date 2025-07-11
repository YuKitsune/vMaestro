using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class InsertOvershootFlightHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    readonly IClock _clock = new FixedClock(new DateTimeOffset(2025, 06, 29, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task WhenInsertingAFlight_BehindAnotherFlight_ThenFlightIsInserted()
    {
        // Arrange
        var overshootFlight = new FlightBuilder("QFA1")
            .WithLandingTime(_clock.UtcNow())
            .WithState(State.Landed)
            .Build();

        var landingFlight = new FlightBuilder("QFA2")
            .WithLandingTime(_clock.UtcNow().AddMinutes(5))
            .WithState(State.Frozen)
            .Build();

        var sequence = new Sequence(airportConfigurationFixture.Instance);
        sequence.Add(overshootFlight);
        sequence.Add(landingFlight);

        sequence.ChangeRunwayMode(new RunwayMode
        {
            Identifier = "34",
            Runways =
            [
                new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = 180 }
            ]
        });

        var handler = GetRequestHandler(sequence);
        var request = new InsertOvershootFlightRequest(
            "YSSY",
            overshootFlight.Callsign,
            InsertionPoint.After,
            landingFlight.Callsign);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);

        overshootFlight.State.ShouldBe(State.Frozen);
        overshootFlight.ScheduledLandingTime.ShouldBe(landingFlight.ScheduledLandingTime.AddMinutes(3));
    }

    [Fact]
    public async Task WhenInsertingAFlight_InFrontOfAnotherFlight_ThenFlightIsInserted()
    {
        // Arrange
        var overshootFlight = new FlightBuilder("QFA1")
            .WithLandingTime(_clock.UtcNow())
            .WithState(State.Landed)
            .Build();

        var landingFlight = new FlightBuilder("QFA2")
            .WithLandingTime(_clock.UtcNow().AddMinutes(5))
            .WithState(State.Frozen)
            .Build();

        var sequence = new Sequence(airportConfigurationFixture.Instance);
        sequence.Add(overshootFlight);
        sequence.Add(landingFlight);

        sequence.ChangeRunwayMode(new RunwayMode
        {
            Identifier = "34",
            Runways =
            [
                new RunwayConfiguration { Identifier = "34L", LandingRateSeconds = 180 }
            ]
        });

        var handler = GetRequestHandler(sequence);
        var request = new InsertOvershootFlightRequest(
            "YSSY",
            overshootFlight.Callsign,
            InsertionPoint.Before,
            landingFlight.Callsign);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Order().Select(f => f.Callsign).ToArray().ShouldBe(["QFA2", "QFA1"]);

        overshootFlight.State.ShouldBe(State.Frozen);
        overshootFlight.ScheduledLandingTime.ShouldBe(landingFlight.ScheduledLandingTime.AddMinutes(-3));
    }


    InsertFlightOvershootRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.CanSequenceFor(Arg.Is("YSSY")).Returns(true);
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));

        var mediator = Substitute.For<IMediator>();
        return new InsertFlightOvershootRequestHandler(sequenceProvider, mediator);
    }
}
