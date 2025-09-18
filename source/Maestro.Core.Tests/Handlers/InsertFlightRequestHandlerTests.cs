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

// TODO: Move relevant Sequence tests to this class

public class InsertFlightRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture, PerformanceLookupFixture performanceLookupFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    static readonly TimeSpan _landingRate = TimeSpan.FromSeconds(180);

    readonly RunwayMode _runwayMode = new(
        new RunwayModeConfiguration
        {
            Identifier = "34IVA",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = (int)_landingRate.TotalSeconds
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = (int)_landingRate.TotalSeconds
                }
            ]
        });

    // Simulating the user clicking on the right ladder to avoid getting the default runways
    readonly string[] _requestedRunways = new[] { "07", "16L", "34R" };

    [Fact]
    public async Task WhenInsertingAFlight_ItShouldBeSequencedAtTheTargetTimeWithOneOfTheSpecifiedRunways()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new ExactInsertionOptions(targetTime, _requestedRunways));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        insertedFlight.LandingTime.ShouldBe(targetTime);
        insertedFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        insertedFlight.ManualLandingTime.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenInsertingAFlightBeforeAnotherOne_ItShouldBeSequencedAtTheTargetTimeWithTheSameRunway()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var referenceFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(referenceFlight, referenceFlight.LandingTime);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new RelativeInsertionOptions(referenceFlight.Callsign, RelativePosition.Before));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        insertedFlight.LandingTime.ShouldBe(referenceFlight.LandingTime.Subtract(_landingRate));
        insertedFlight.AssignedRunwayIdentifier.ShouldBe(referenceFlight.AssignedRunwayIdentifier);
    }

    [Fact]
    public async Task WhenInsertingAFlightAfterAnotherOne_ItShouldBeSequencedSlightlyAfterTheTargetTimeWithTheSameRunway()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var referenceFlight = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(referenceFlight, referenceFlight.LandingTime);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new RelativeInsertionOptions(referenceFlight.Callsign, RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        insertedFlight.LandingTime.ShouldBe(referenceFlight.LandingTime.Add(_landingRate));
        insertedFlight.AssignedRunwayIdentifier.ShouldBe(referenceFlight.AssignedRunwayIdentifier);
    }

    [Fact]
    public async Task WhenInsertingAFlightWithBlankCallsign_ADummyFlightShouldBeCreated()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();

        var initialFlightCount = sequence.Flights.Count;

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "",
            null,
            new ExactInsertionOptions(targetTime, _requestedRunways));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(initialFlightCount + 1);
        var dummyFlight = sequence.Flights.Last();
        dummyFlight.Callsign.ShouldMatch("\\*{4}\\d{2}\\*");
        dummyFlight.LandingTime.ShouldBe(targetTime);
        dummyFlight.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public async Task WhenInsertingAFlightThatHasLanded_ItIsResequencedAtTheSpecifiedTimeOnTheSpecifiedRunway()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var landedFlight = new FlightBuilder("QFA123")
            .WithState(State.Landed)
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34R")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(landedFlight, landedFlight.LandingTime);

        var handler = GetRequestHandler(sequence);
        var request = new InsertOvershootRequest(
            "YSSY",
            "QFA123",
            new ExactInsertionOptions(targetTime, _requestedRunways));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        landedFlight.State.ShouldBe(State.Frozen);
        landedFlight.LandingTime.ShouldBe(targetTime);
        landedFlight.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public async Task WhenInsertingAFlightFromThePendingList_ItIsSequencedAtTheSpecifiedTimeOnTheSpecifiedRunway()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var pendingFlight = new FlightBuilder("QFA123")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(pendingFlight, pendingFlight.LandingEstimate);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new ExactInsertionOptions(targetTime, _requestedRunways));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.State.ShouldBe(State.Unstable);
        pendingFlight.LandingTime.ShouldBe(targetTime);
        pendingFlight.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public async Task WhenInsertingAFlightThatDoesNotExist_ItShouldBeCreatedWithTheSpecifiedDetails()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var targetTime = now.AddMinutes(10);

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();

        var initialFlightCount = sequence.Flights.Count;
        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new ExactInsertionOptions(targetTime, _requestedRunways));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(initialFlightCount + 1);
        var newFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        newFlight.AircraftType.ShouldBe("B738");
        newFlight.WakeCategory.ShouldBe(WakeCategory.Medium);
        newFlight.LandingTime.ShouldBe(targetTime);
        newFlight.AssignedRunwayIdentifier.ShouldBe("34R");
    }

    [Fact]
    public async Task WhenInsertingAFlightBeforeAFrozenFlight_ItShouldThrowAnException()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozenFlight = new FlightBuilder("QFA456")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(frozenFlight, frozenFlight.LandingTime);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new RelativeInsertionOptions(frozenFlight.Callsign, RelativePosition.Before));

        // Act / Assert
        var exception = await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
        exception.Message.ShouldBe("Flights cannot be inserted before a frozen flight");
    }

    [Fact]
    public async Task WhenInsertingAFlightAfterAFrozenFlight_ItShouldBeSequencedAtTheFrozenFlightTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var frozenFlight = new FlightBuilder("QFA456")
            .WithState(State.Frozen)
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();
        sequence.Insert(frozenFlight, frozenFlight.LandingTime);

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new RelativeInsertionOptions(frozenFlight.Callsign, RelativePosition.After));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var insertedFlight = sequence.Flights.Single(f => f.Callsign == "QFA123");
        insertedFlight.LandingTime.ShouldBe(frozenFlight.LandingTime.Add(_landingRate));
        insertedFlight.AssignedRunwayIdentifier.ShouldBe(frozenFlight.AssignedRunwayIdentifier);
    }

    [Fact]
    public async Task WhenInsertingAFlightWithReferenceCallsignNotFound_ItShouldThrowAnException()
    {
        // Arrange
        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithRunwayMode(_runwayMode)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertFlightRequest(
            "YSSY",
            "QFA123",
            "B738",
            new RelativeInsertionOptions("NONEXISTENT", RelativePosition.Before));

        // Act / Assert
        var exception = await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
        exception.Message.ShouldBe("Reference flight NONEXISTENT not found");
    }

    InsertFlightRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sessionManager = new MockLocalSessionManager(sequence);
        var mediator = Substitute.For<IMediator>();
        return new InsertFlightRequestHandler(sessionManager, clockFixture.Instance, performanceLookupFixture.Instance, mediator);
    }
}
