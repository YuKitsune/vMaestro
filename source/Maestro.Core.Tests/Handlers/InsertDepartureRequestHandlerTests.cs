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

public class InsertDepartureRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    PerformanceLookupFixture performanceLookupFixture,
    ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    readonly TimeSpan _arrivalDuration = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task WhenInsertingAFlight_ItShouldBeBecomeStable()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);
        var flightTime = TimeSpan.FromMinutes(20);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithState(State.Pending)
            .WithEstimatedFlightTime(flightTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(pendingFlight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.State.ShouldBe(State.Stable);
    }

    [Fact]
    public async Task WhenInsertingAFlight_LandingEstimateShouldBeCalculatedBasedOnTakeOffTimeAndFlightTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);
        var flightTime = TimeSpan.FromMinutes(20);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithState(State.Pending)
            .WithEstimatedFlightTime(flightTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(pendingFlight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var expectedLandingTime = takeOffTime.Add(flightTime);
        pendingFlight.EstimatedLandingTime.ShouldBe(expectedLandingTime);
    }

    [Fact]
    public async Task WhenInsertingAFlight_FeederFixEstimateShouldBeCalculatedBasedOnTakeOffTimeAndFlightTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);
        var flightTime = TimeSpan.FromMinutes(20);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithState(State.Pending)
            .WithEstimatedFlightTime(flightTime)
            .WithFeederFix("RIVET")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(pendingFlight)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        pendingFlight.EstimatedFeederFixTime.ShouldBe(takeOffTime.Add(flightTime).Add(-_arrivalDuration));
    }

    [Fact]
    public async Task WhenInsertingAFlight_FlightNotFoundInPendingList_ShouldThrowException()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(30);

        var sequence = new SequenceBuilder(_airportConfiguration).Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Act / Assert
        var exception = await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
        exception.Message.ShouldBe("QFA1 was not found in the pending list.");
    }

    [Fact]
    public async Task WhenInsertingAFlightWithNullEstimatedTimeEnroute_ShouldThrowException()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);

        var pendingFlightWithoutEte = new FlightBuilder("QFA1")
            .WithState(State.Pending)
            .Build(); // No ETE set

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(pendingFlightWithoutEte)
            .Build();

        var handler = GetRequestHandler(sequence);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Act / Assert
        var exception = await Should.ThrowAsync<MaestroException>(() => handler.Handle(request, CancellationToken.None));
        exception.Message.ShouldBe("QFA1 does not have an ETE.");
    }

    [Fact]
    public async Task WhenInsertingAFlight_SchedulerShouldBeCalled()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);
        var flightTime = TimeSpan.FromMinutes(20);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithState(State.Pending)
            .WithEstimatedFlightTime(flightTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(pendingFlight)
            .Build();

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetRequestHandler(sequence, scheduler);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        scheduler.Received(1).Schedule(Arg.Is(sequence));
    }

    [Fact]
    public async Task WhenInsertingAFlight_ItShouldBecomeStable()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);
        var flightTime = TimeSpan.FromMinutes(20);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithState(State.Pending)
            .WithEstimatedFlightTime(flightTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(pendingFlight)
            .Build();

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetRequestHandler(sequence, scheduler);
        var request = new InsertDepartureRequest(
            "YSSY",
            "QFA1",
            "B738",
            "YSCB",
            takeOffTime);

        // Verify initial state
        pendingFlight.State.ShouldBe(State.Pending);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert - Final state should be Stable
        pendingFlight.State.ShouldBe(State.Stable);
    }

    InsertDepartureRequestHandler GetRequestHandler(Sequence sequence, IScheduler? scheduler = null)
    {
        var sequenceProvider = new MockSequenceProvider(sequence);

        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetArrivalInterval(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<AircraftPerformanceData>())
            .Returns(_arrivalDuration);

        scheduler ??= Substitute.For<IScheduler>();
        return new InsertDepartureRequestHandler(
            sequenceProvider,
            performanceLookupFixture.Instance,
            arrivalLookup,
            scheduler,
            clockFixture.Instance);
    }
}
