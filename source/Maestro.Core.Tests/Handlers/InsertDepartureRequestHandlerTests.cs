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
            .WithEstimatedFlightTime(flightTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.AddPendingFlight(pendingFlight);

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
            .WithEstimatedFlightTime(flightTime)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.AddPendingFlight(pendingFlight);

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
        pendingFlight.LandingEstimate.ShouldBe(expectedLandingTime);
    }

    [Fact]
    public async Task WhenInsertingAFlight_FeederFixEstimateShouldBeCalculatedBasedOnTakeOffTimeAndFlightTime()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var takeOffTime = now.AddMinutes(5);
        var flightTime = TimeSpan.FromMinutes(20);

        var pendingFlight = new FlightBuilder("QFA1")
            .WithEstimatedFlightTime(flightTime)
            .WithFeederFix("RIVET")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.AddPendingFlight(pendingFlight);

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
        pendingFlight.FeederFixEstimate.ShouldBe(takeOffTime.Add(flightTime).Add(-_arrivalDuration));
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
            .Build(); // No ETE set

        var sequence = new SequenceBuilder(_airportConfiguration).Build();
        sequence.AddPendingFlight(pendingFlightWithoutEte);

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

    // TODO: This is covered in SequenceTests. Consider moving relevent tests into here.
    // [Fact]
    // public async Task WhenInsertingAFlight_ItShouldBeScheduled()
    // {
    //     // Arrange
    //     var now = clockFixture.Instance.UtcNow();
    //     var takeOffTime = now.AddMinutes(5);
    //     var flightTime = TimeSpan.FromMinutes(20);
    //
    //     var pendingFlight = new FlightBuilder("QFA1")
    //         .WithEstimatedFlightTime(flightTime)
    //         .Build();
    //
    //     var sequence = new SequenceBuilder(_airportConfiguration).Build();
    //     sequence.AddPendingFlight(pendingFlight);
    //
    //     var handler = GetRequestHandler(sequence, scheduler);
    //     var request = new InsertDepartureRequest(
    //         "YSSY",
    //         "QFA1",
    //         "B738",
    //         "YSCB",
    //         takeOffTime);
    //
    //     // Act
    //     await handler.Handle(request, CancellationToken.None);
    //
    //     // Assert
    //     scheduler.Received(1).Schedule(Arg.Is(sequence));
    // }

    InsertDepartureRequestHandler GetRequestHandler(Sequence sequence)
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
            .Returns(_arrivalDuration);

        var mediator = Substitute.For<IMediator>();

        return new InsertDepartureRequestHandler(
            sessionManager,
            performanceLookupFixture.Instance,
            arrivalLookup,
            clockFixture.Instance,
            mediator);
    }
}
