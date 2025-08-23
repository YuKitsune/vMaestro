using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class InsertDepartureRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

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

        var timeFromFeederFixToRunway = TimeSpan.FromMinutes(10);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        var expectedFeederFixTime = takeOffTime.Add(flightTime).Subtract(timeFromFeederFixToRunway);
        pendingFlight.EstimatedFeederFixTime.ShouldBe(expectedFeederFixTime);
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

    InsertDepartureRequestHandler GetRequestHandler(Sequence sequence, IScheduler? scheduler = null)
    {
        var sequenceProvider = new MockSequenceProvider(sequence);
        scheduler ??= Substitute.For<IScheduler>();
        return new InsertDepartureRequestHandler(sequenceProvider, scheduler, clockFixture.Instance);
    }
}
